using System;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <inheritdoc />
    /// <summary>
    ///     Holds the options controlling how <see cref="DelimitedReader" /> and <see cref="DelimitedWriter" />
    ///     read and write a delimited file.
    /// </summary>
    public sealed class DelimitedOptions : IOptions
    {
        /// <summary>
        ///     Initialises a new instance of DelimitedOptions.
        /// </summary>
        public DelimitedOptions()
        {
        }

        /// <summary>
        ///     Gets or sets the character or characters used to separate the columns.
        /// </summary>
        public string Separator
        {
            get;
            set
            {
                if (string.IsNullOrEmpty( value ))
                {
                    throw new ArgumentException( Resources.EmptySeparator );
                }
                field = value;
            }
        } = ",";

        /// <summary>
        ///     Gets or sets the character or characters used to separate the records.
        /// </summary>
        /// <remarks>
        ///     By default, FlatFiles looks for any of <c>\r</c>, <c>\n</c> or <c>\r\n</c>, so a file may mix
        ///     them; setting this to null restores that. When writing, the default is
        ///     <see cref="Environment.NewLine" />.
        /// </remarks>
        public string? RecordSeparator { get; set; }

        /// <summary>
        ///     Gets or sets the character used to quote a value that contains a separator, a quote or a line break.
        /// </summary>
        public char Quote { get; set; } = '"';

        /// <summary>
        ///     Gets or sets how FlatFiles will handle quoting values.
        /// </summary>
        public QuoteBehaviour QuoteBehaviour 
        {
            get;
            set
            {
                if (!Enum.IsDefined( value ))
                {
                    throw new ArgumentException( Resources.InvalidQuoteBehaviour, nameof( value ) );
                }
                field = value;
            }
        } = QuoteBehaviour.Default;

        /// <summary>
        ///     Gets or sets whether the first record is the schema.
        /// </summary>
        public bool IsFirstRecordSchema { get; set; }

        /// <summary>
        ///     Gets or sets whether leading and trailing whitespace should be preserved when reading.
        /// </summary>
        public bool PreserveWhiteSpace { get; set; }

        /// <summary>
        ///     Gets or sets whether column-level metadata should be disabled for non-metadata columns.
        /// </summary>
        public bool IsColumnContextDisabled { get; set; }

        /// <summary>
        ///     Gets or sets whether the raw text of each record should be kept and reported through
        /// <see cref="IRecordContext.Record" />.
        /// </summary>
        /// <remarks>
        /// <para>
        ///     This defaults to <c>false</c>, so the raw text is discarded and
        /// <see cref="IRecordContext.Record" /> reports an empty string. Set it to <c>true</c> to
        ///     get the text back.
        /// </para>
        /// <para>
        ///     Nothing in parsing needs it. The reader takes each value straight from the characters it has
        ///     buffered, and a schema selector is handed those values rather than the text, so the string this
        ///     builds exists only to be reported. Measured over 10,000 records of thirteen columns, a read
        ///     allocated 741 bytes a record with the option off and 1,035 with it on. Throughput is unchanged
        ///     either way, so what it costs is allocation and the collection pressure that comes with it, which
        ///     is why the default is off.
        /// </para>
        /// <para>
        ///     Fixed-length files are unaffected: that reader takes its column values out of the record
        ///     text, so it always has the text and always reports it.
        /// </para>
        /// </remarks>
        public bool PreserveRecordText { get; set; }

        /// <summary>
        ///     Gets or sets the global, default format provider.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        ///     Duplicates the options.
        /// </summary>
        /// <returns>The new options.</returns>
        public DelimitedOptions Clone()
        {
            return (DelimitedOptions) MemberwiseClone();
        }
    }
}
