using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Drives the same value through every way a column can be asked to format one, and insists they agree.
    /// </summary>
    /// <remarks>
    ///     The mirror of <see cref="ParsePathAgreementTester" />, and here for the same reason. A column has three
    ///     entry points on this side too - the one that returns a string, the one that writes into a buffer, and
    ///     the typed write a mapped member uses - and which one a record takes is decided by things a caller sets
    ///     for unrelated reasons. Nothing compared them.
    ///     <para>
    ///         Both shapes of column are run: one that can only format to a string, and one that writes into the
    ///         buffer itself. Most columns are the first; the ones that matter for speed are the second; and the
    ///         buffer path exists to let them be, so the two have to say the same thing.
    ///     </para>
    /// </remarks>
    [TestClass]
    public class FormatPathAgreementTester
    {
        private static readonly object?[] Values = [42, -1, null];

        [TestMethod]
        public void TestEveryPathAgrees()
        {
            var disagreements = new List<string>();
            foreach (var writesToBuffer in new[] { true, false })
            {
                foreach (var hook in new[] { Hook.None, Hook.Formatting, Hook.Formatted, Hook.Both })
                {
                    foreach (var hasNullText in new[] { true, false })
                    {
                        foreach (var value in Values)
                        {
                            Compare( writesToBuffer, hook, hasNullText, value, disagreements );
                        }
                    }
                }
            }

            Assert.IsEmpty( disagreements, string.Join( Environment.NewLine, disagreements ) );
        }

        private enum Hook
        {
            None,
            Formatting,
            Formatted,
            Both
        }

        private static void Compare( bool writesToBuffer, Hook hook, bool hasNullText, object? value,
            List<string> disagreements )
        {
            var shape = $"buffer={writesToBuffer} hook={hook} nullText={hasNullText} value={value ?? "null"}";

            var asString = Answer( () => Column( writesToBuffer, hook, hasNullText ).Format( null, value ) );
            var intoBuffer = Answer( () => Written( Column( writesToBuffer, hook, hasNullText ), value ) );

            if (asString != intoBuffer)
            {
                disagreements.Add( $"the string gave {asString} and the buffer gave {intoBuffer} for {shape}" );
            }

            // The typed write is only available where no hook deals in objects or strings, which is what
            // SupportsTypedFormat says. Where it is available it has to agree with the other two.
            var typed = Column( writesToBuffer, hook, hasNullText );
            if (!typed.SupportsTypedFormatForTest)
            {
                return;
            }
            var fromTyped = Answer( () => typed.WriteTypedForTest( value ) );
            if (fromTyped != asString)
            {
                disagreements.Add( $"the typed write gave {fromTyped} and the string gave {asString} for {shape}" );
            }
        }

        /// <summary>What a path wrote, as text, so that a thrown exception compares like any other answer.</summary>
        private static string Answer( Func<string?> format )
        {
            try
            {
                return format() ?? "null";
            }
            catch (Exception exception)
            {
                return exception.GetType().Name;
            }
        }

        private static string Written( ProbeColumn column, object? value )
        {
            var buffer = new ArrayBufferWriter<char>();
            column.Format( null, value, buffer );
            return buffer.WrittenSpan.ToString();
        }

        private static ProbeColumn Column( bool writesToBuffer, Hook hook, bool hasNullText )
        {
            ProbeColumn column = writesToBuffer ? new BufferProbeColumn( "Probe" ) : new ProbeColumn( "Probe" );
            if (hasNullText)
            {
                column.NullFormatter = NullFormatter.ForValue( "(none)" );
            }
            if (hook is Hook.Formatting or Hook.Both)
            {
                // Including a value it turns into nothing, since what the null formatter then does is a decision
                // each path makes for itself.
                column.OnFormatting = ( _, given ) => given is -1 ? null : given;
            }
            if (hook is Hook.Formatted or Hook.Both)
            {
                column.OnFormatted = ( _, text ) => $"[{text}]";
            }
            return column;
        }

        /// <summary>A column that can only format to a string, which is what most of them are.</summary>
        private class ProbeColumn( string name ) : ColumnDefinition<int>( name )
        {
            internal bool SupportsTypedFormatForTest => SupportsTypedFormat;

            internal string WriteTypedForTest( object? value )
            {
                var buffer = new ArrayBufferWriter<char>();
                if (value is null)
                {
                    // What a mapped member holding nothing does, the one case the typed path decides itself.
                    FormatNull( null, buffer );
                }
                else
                {
                    FormatTyped( null, (int) value, buffer );
                }
                return buffer.WrittenSpan.ToString();
            }

            protected override int OnParse( IColumnContext? context, string value )
            {
                return int.Parse( value, NumberStyles.None, CultureInfo.InvariantCulture );
            }

            protected override string OnFormat( IColumnContext? context, int value )
            {
                return value.ToString( CultureInfo.InvariantCulture );
            }
        }

        /// <summary>A column that writes into the buffer itself, as the ones built for speed do.</summary>
        private sealed class BufferProbeColumn( string name ) : ProbeColumn( name )
        {
            protected override void OnFormat( IColumnContext? context, int value, IBufferWriter<char> destination )
            {
                WriteFormatted( destination, value, null, CultureInfo.InvariantCulture );
            }
        }
    }
}
