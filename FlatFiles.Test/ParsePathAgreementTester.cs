using System;
using System.Collections.Generic;
using System.Globalization;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Drives the same value through every way a column can be asked to parse one, and insists they agree.
    /// </summary>
    /// <remarks>
    ///     A column has three entry points: the string overload, the span overload, and the typed parse a mapped
    ///     property uses. They once each decided for themselves what a blank value meant and when a hook ran, and
    ///     the library shipped two defects from the three drifting apart. They now share one decision; this is what
    ///     holds them to it.
    ///     <para>
    ///         Every case is run against every combination of the things that alter the answer - nullable or not, a
    ///         default value or none, trimmed or not, a hook or none - because the defects both lived in a
    ///         combination rather than in one setting.
    ///     </para>
    /// </remarks>
    [TestClass]
    public class ParsePathAgreementTester
    {
        private static readonly string[] Values =
        [
            "42",        // an ordinary value
            "  42  ",    // one the trim has something to do to
            "",          // empty
            "   ",       // blank
            "#42",       // one only a hook can read
            "OPEN"       // one a hook answers by writing nothing
        ];

        [TestMethod]
        public void TestEveryPathAgrees()
        {
            var disagreements = new List<string>();
            foreach (var isNullable in new[] { true, false })
            {
                foreach (var hasDefault in new[] { true, false })
                {
                    foreach (var isTrimmed in new[] { true, false })
                    {
                        foreach (var hook in new[] { Hook.None, Hook.Span, Hook.String })
                        {
                            foreach (var value in Values)
                            {
                                Compare( isNullable, hasDefault, isTrimmed, hook, value, disagreements );
                            }
                        }
                    }
                }
            }

            Assert.IsEmpty( disagreements, string.Join( Environment.NewLine, disagreements ) );
        }

        private enum Hook
        {
            None,
            Span,
            String
        }

        private static void Compare( bool isNullable, bool hasDefault, bool isTrimmed, Hook hook, string value,
            List<string> disagreements )
        {
            var shape = $"nullable={isNullable} default={hasDefault} trimmed={isTrimmed} hook={hook} value='{value}'";

            var fromString = Answer( () => Column( isNullable, hasDefault, isTrimmed, hook ).Parse( null, value ) );
            var fromSpan = Answer( () => Column( isNullable, hasDefault, isTrimmed, hook ).Parse( null, value.AsSpan() ) );

            if (fromString != fromSpan)
            {
                disagreements.Add( $"string gave {fromString} and span gave {fromSpan} for {shape}" );
            }

            // The typed path is only available where no hook deals in strings or objects, which is what
            // SupportsTypedParse says. Where it is available it has to agree with the other two.
            var typed = Column( isNullable, hasDefault, isTrimmed, hook );
            if (!typed.SupportsTypedParseForTest)
            {
                return;
            }
            var fromTyped = Answer( () => typed.ParseTypedForTest( null, value.AsSpan() ) );
            if (fromTyped != fromString)
            {
                disagreements.Add( $"typed gave {fromTyped} and string gave {fromString} for {shape}" );
            }
        }

        /// <summary>What a path answered, as text, so that a thrown exception compares like any other answer.</summary>
        private static string Answer( Func<object?> parse )
        {
            try
            {
                var value = parse();
                return value is null ? "null" : Convert.ToString( value, CultureInfo.InvariantCulture )!;
            }
            catch (Exception exception)
            {
                return exception.GetType().Name;
            }
        }

        private static ProbeColumn Column( bool isNullable, bool hasDefault, bool isTrimmed, Hook hook )
        {
            var column = new ProbeColumn( "Probe", isTrimmed )
            {
                IsNullable = isNullable,
                DefaultValue = hasDefault ? DefaultValue.Use( -1 ) : DefaultValue.Disabled()
            };
            switch (hook)
            {
                case Hook.Span:
                    column.OnParsingSpan = StripHashOrBlankOpen;
                    break;
                case Hook.String:
                    column.OnParsing = ( _, text ) => StripHashOrBlank( text );
                    break;
            }
            return column;
        }

        /// <summary>The span hook and the string hook below have to mean the same thing, or nothing is proved.</summary>
        private static int StripHashOrBlankOpen( IColumnContext? context, ReadOnlySpan<char> value, Span<char> destination )
        {
            var trimmed = value.Trim();
            if (trimmed.SequenceEqual( "OPEN" ))
            {
                return 0;
            }
            if (trimmed.StartsWith( "#" ))
            {
                trimmed[1..].CopyTo( destination );
                return trimmed.Length - 1;
            }
            return SpanParsingHooks.Unchanged;
        }

        private static string StripHashOrBlank( string value )
        {
            var trimmed = value.Trim();
            if (trimmed == "OPEN")
            {
                return string.Empty;
            }
            return trimmed.StartsWith( '#' ) ? trimmed[1..] : value;
        }

        /// <summary>An ordinary typed column, with the internals this test needs reachable.</summary>
        private sealed class ProbeColumn( string name, bool isTrimmed ) : ColumnDefinition<int>( name )
        {
            protected override bool IsTrimmed => isTrimmed;

            internal bool SupportsTypedParseForTest => SupportsTypedParse;

            internal object? ParseTypedForTest( IColumnContext? context, ReadOnlySpan<char> value )
            {
                return ParseTyped( context, value, out var parsed ) ? parsed : null;
            }

            /// <remarks>
            ///     Deliberately strict about whitespace. The ordinary <c>int.Parse</c> accepts it either side, so
            ///     a column using one cannot tell whether it was trimmed - and a test built on that would pass
            ///     whether or not the paths trimmed alike, which is one of the things this is here to check.
            /// </remarks>
            protected override int OnParse( IColumnContext? context, string value )
            {
                return int.Parse( value, NumberStyles.None, CultureInfo.InvariantCulture );
            }

            protected override string OnFormat( IColumnContext? context, int value )
            {
                return value.ToString( CultureInfo.InvariantCulture );
            }
        }
    }
}
