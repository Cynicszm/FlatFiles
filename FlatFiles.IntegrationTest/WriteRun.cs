using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FlatFiles.TypeMapping;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     The records a write scenario writes, and which record layout each one came from.
    /// </summary>
    /// <remarks>
    ///     Loaded from the sample before the measurement starts, because what is being measured is the write. It
    ///     costs several hundred megabytes on the larger samples - the whole file is held as values - so the peak
    ///     memory a write scenario reports is mostly this rather than anything the writer does. Allocation is
    ///     measured from the moment the write begins and is therefore clean.
    /// </remarks>
    internal sealed class WriteSource
    {
        public List<object?[]> Values { get; } = [];

        public List<MappedRecord> Entities { get; } = [];

        /// <summary>
        ///     Which layout each record belongs to, for a fixed-length sample; empty for a delimited one, which
        ///     has only the one schema.
        /// </summary>
        public List<int> Layouts { get; } = [];

        public long Records => Entities.Count != 0 ? Entities.Count : Values.Count;
    }

    /// <summary>
    ///     Writes a sample back out and reports what that cost.
    /// </summary>
    /// <remarks>
    ///     Three scenarios, mirroring the reading ones. <c>write-text</c> writes every value as text through a
    ///     schema of string columns, which is the floor; <c>write-typed</c> writes the same records with each
    ///     column given its own type, so the difference is what formatting a value costs over copying it; and
    ///     <c>write-mapper</c> writes entities through a type mapper, which is the only one that reads a member
    ///     off an entity and the only one the getters a mapper builds per column can be seen in.
    ///     <para>
    ///         A fixed-length sample of several layouts is written through an injector, one schema per layout,
    ///         which is what a caller with such a file writes. Which layout a record belongs to was settled when
    ///         the source was loaded, so the predicate the injector runs is an array lookup: the records have to
    ///         come out in the order they went in for the file to be comparable with the one they came from.
    ///     </para>
    /// </remarks>
    internal static class WriteRun
    {
        public const string Text = "write-text";

        public const string Typed = "write-typed";

        public const string Mapped = "write-mapper";

        public static bool IsWrite( string scenario )
        {
            return scenario is Text or Typed or Mapped;
        }

        /// <summary>
        ///     Where a scenario writes to: beside the sample, and deleted once it has been measured and compared.
        /// </summary>
        public static string DestinationFor( string path )
        {
            return path + ".written";
        }

        // ------------------------------------------------------------------ loading

        /// <summary>
        ///     Reads the sample into memory, which is not measured. A fixed-length sample is read twice: once as
        ///     text to settle which layout each record belongs to, and once through the selector for the records
        ///     themselves.
        /// </summary>
        public static WriteSource Load( FileProfile profile, string path, string scenario )
        {
            var source = new WriteSource();
            if (profile.IsFixedLength)
            {
                LoadLayouts( profile, path, source );
            }
            using var stream = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20 );
            using var text = new StreamReader( stream );
            if (scenario == Mapped)
            {
                LoadEntities( profile, text, source );
            }
            else
            {
                LoadValues( profile, text, scenario == Typed, source );
            }
            // The layouts were counted from the file's lines and the records from what the reader yielded, and
            // the write reads one by the index of the other. A sample where those disagree would quietly write
            // records under the wrong layout, so it stops here instead.
            if (profile.IsFixedLength && source.Layouts.Count != source.Records)
            {
                throw new InvalidOperationException(
                    $"{profile.Name} has {source.Layouts.Count} records by its text and {source.Records} by its reader, so which layout each belongs to cannot be settled." );
            }
            return source;
        }

        private static void LoadLayouts( FileProfile profile, string path, WriteSource source )
        {
            var predicates = new Func<string, bool>[profile.RecordTypes.Count];
            for (var index = 0; index != predicates.Length; ++index)
            {
                predicates[index] = SchemaFactory.Predicate( profile.RecordTypes[index] );
            }
            using var reader = new StreamReader( path );
            for (var record = reader.ReadLine(); record is not null; record = reader.ReadLine())
            {
                if (record.Length == 0)
                {
                    continue;
                }
                source.Layouts.Add( FirstMatch( predicates, record ) );
            }
        }

        private static int FirstMatch( Func<string, bool>[] predicates, string record )
        {
            for (var index = 0; index != predicates.Length; ++index)
            {
                if (predicates[index]( record ))
                {
                    return index;
                }
            }
            // The samples are built so that every record matches a layout, and the reading scenarios refuse one
            // that does not. Falling back to the first keeps the counts in step if that ever stops being true.
            return 0;
        }

        private static void LoadValues( FileProfile profile, TextReader text, bool typed, WriteSource source )
        {
            IReader reader = profile.IsFixedLength
                ? new FixedLengthReader( text, SchemaFactory.CreateSelector( profile, typed ), FixedLengthOptionsFor( profile ) )
                : new DelimitedReader( text, SchemaFactory.Create( profile, typed ), DelimitedOptionsFor( profile ) );
            reader.RecordError += ( _, e ) => e.IsHandled = true;
            while (reader.Read())
            {
                source.Values.Add( reader.GetValues() );
            }
        }

        private static void LoadEntities( FileProfile profile, TextReader text, WriteSource source )
        {
            if (profile.IsFixedLength)
            {
                var reader = MapperFactory.CreateSelector( profile ).GetReader( text, FixedLengthOptionsFor( profile ) );
                reader.RecordError += ( _, e ) => e.IsHandled = true;
                while (reader.Read())
                {
                    source.Entities.Add( (MappedRecord) reader.Current! );
                }
                return;
            }
            var delimited = MapperFactory.Create( profile ).GetReader( text, DelimitedOptionsFor( profile ) );
            delimited.RecordError += ( _, e ) => e.IsHandled = true;
            while (delimited.Read())
            {
                source.Entities.Add( delimited.Current );
            }
        }

        // ------------------------------------------------------------------ writing

        /// <summary>
        ///     Writes every record once. Everything a job pays for is inside here, as it is for a read: the
        ///     schema being built, the writer being constructed, the file being created and flushed, and the
        ///     runtime compiling the format path on first use.
        /// </summary>
        public static void Write( FileProfile profile, string destination, string scenario, WriteSource source )
        {
            Directory.CreateDirectory( Path.GetDirectoryName( destination )! );
            // The framework's own buffer sizes: see the note on the reading side. A megabyte apiece here came to
            // about three megabytes inside every write measurement, which on the sample of 1,790 records was 3,601
            // of the 3,689 bytes a record it reported - and which hid a regression of fifty bytes a record inside
            // the two percent the check allows.
            using (var stream = new FileStream( destination, FileMode.Create, FileAccess.Write, FileShare.None ))
            using (var text = new StreamWriter( stream, new UTF8Encoding( false ) ))
            {
                if (scenario == Mapped)
                {
                    WriteEntities( profile, text, source );
                }
                else
                {
                    WriteValues( profile, text, scenario == Typed, source );
                }
            }
        }

        private static void WriteValues( FileProfile profile, TextWriter text, bool typed, WriteSource source )
        {
            if (profile.IsFixedLength)
            {
                var written = 0;
                var injector = new FixedLengthSchemaInjector();
                for (var layout = 0; layout != profile.RecordTypes.Count; ++layout)
                {
                    var schema = SchemaFactory.CreateLayout( profile.RecordTypes[layout], typed );
                    var index = layout;
                    injector.When( _ => source.Layouts[written] == index ).Use( schema );
                }
                var fixedLength = new FixedLengthWriter( text, injector, FixedLengthOptionsFor( profile ) );
                foreach (var values in source.Values)
                {
                    fixedLength.Write( values );
                    ++written;
                }
                return;
            }
            var writer = new DelimitedWriter( text, SchemaFactory.Create( profile, typed ), DelimitedOptionsFor( profile ) );
            foreach (var values in source.Values)
            {
                writer.Write( values );
            }
        }

        private static void WriteEntities( FileProfile profile, TextWriter text, WriteSource source )
        {
            if (profile.IsFixedLength)
            {
                var written = 0;
                var injector = MapperFactory.CreateInjector( profile, () => source.Layouts[written] );
                var typedWriter = injector.GetWriter( text, FixedLengthOptionsFor( profile ) );
                foreach (var entity in source.Entities)
                {
                    typedWriter.Write( entity );
                    ++written;
                }
                return;
            }
            var writer = MapperFactory.Create( profile ).GetWriter( text, DelimitedOptionsFor( profile ) );
            foreach (var entity in source.Entities)
            {
                writer.Write( entity );
            }
        }

        // ------------------------------------------------------------------ options

        private static DelimitedOptions DelimitedOptionsFor( FileProfile profile )
        {
            return new DelimitedOptions
            {
                Separator = profile.Separator,
                RecordSeparator = profile.RecordSeparator,
                IsFirstRecordSchema = true,
                // The sample was generated with every field quoted or none, and a file written back with
                // different quoting is not the file it came from.
                QuoteBehaviour = profile.QuoteEveryField ? QuoteBehaviour.AlwaysQuote : QuoteBehaviour.Default,
                FormatProvider = CultureInfo.InvariantCulture
            };
        }

        private static FixedLengthOptions FixedLengthOptionsFor( FileProfile profile )
        {
            return new FixedLengthOptions
            {
                RecordSeparator = profile.RecordSeparator,
                FormatProvider = CultureInfo.InvariantCulture
            };
        }

        // ------------------------------------------------------------------ comparison

        /// <summary>
        ///     What was written, by hash, and whether that is the file it was read from.
        /// </summary>
        /// <remarks>
        ///     The hash is what the check gates on, and it is the stronger of the two: it pins the bytes the
        ///     library writes, so a change in any of them fails whether or not the output ever matched the
        ///     sample. It is reproducible because the write scenarios format with the invariant culture, which
        ///     a check run on another machine would otherwise disagree with.
        ///     <para>
        ///         Matching the sample is reported rather than gated, because three of the six samples cannot
        ///         match it and are not meant to. A string column trims, so a field of spaces comes back empty;
        ///         some fixed-length layouts have windows that stop short of the record, so the tail is never
        ///         read; and a ragged final column's padding is not part of its value. What <c>write-typed</c>
        ///         writes is a value it parsed rather than the characters the sample spelled it with, and
        ///         <c>write-mapper</c> maps four members and ignores every other column, so neither is meant to
        ///         reproduce anything.
        ///     </para>
        /// </remarks>
        public static (string Hash, string Verdict) Compare( string source, string written, string scenario )
        {
            var hash = FileStore.Hash( written );
            if (scenario != Text)
            {
                return (hash, "not compared");
            }
            return (hash, FileStore.Hash( source ) == hash ? "same" : "differs");
        }
    }
}
