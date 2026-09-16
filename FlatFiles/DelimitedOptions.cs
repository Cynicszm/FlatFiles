using System;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <inheritdoc />
    /// <summary>
    /// Holds configuration options for the DelimitedParser.
    /// </summary>
    public sealed class DelimitedOptions : IOptions
    {
        private string separator = ",";
        private QuoteBehavior quoteBehavior = QuoteBehavior.Default;

        /// <summary>
        /// Initializes a new instance of a DelimitedParserOptions.
        /// </summary>
        public DelimitedOptions()
        {
        }

        /// <summary>
        /// Gets or sets the character or characters used to separate the columns.
        /// </summary>
        public string Separator
        {
            get => separator;
            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(Resources.EmptySeparator);
                }
                separator = value;
            }
        }

        /// <summary>
        /// Gets or sets the character or characters used to separate the records.
        /// </summary>
        /// <remarks>
        /// By default, FlatFiles will look a combination of /r, /n, or /r/n. Setting
        /// the record separator to null will enable this default behavior. When writing,
        /// FlatFiles will use Environment.NewLine as the default record separator.
        /// </remarks>
        public string? RecordSeparator { get; set; }

        /// <summary>
        /// Gets or sets the character used to quote records containing special characters.
        /// </summary>
        public char Quote { get; set; } = '"';

        /// <summary>
        /// Gets or sets how FlatFiles will handle quoting values.
        /// </summary>
        public QuoteBehavior QuoteBehavior 
        {
            get => quoteBehavior; 
            set
            {
                if (!Enum.IsDefined(value))
                {
                    throw new ArgumentException(Resources.InvalidAlignment, nameof(value));
                }
                quoteBehavior = value;
            }
        }

        /// <summary>
        /// Gets or sets whether the first record is the schema.
        /// </summary>
        public bool IsFirstRecordSchema { get; set; }

        /// <summary>
        /// Gets or sets whether leading and trailing whitespace should be preserved when reading.
        /// </summary>
        public bool PreserveWhiteSpace { get; set; }

        /// <summary>
        /// Gets whether column-level metadata should be disabled for non-metadata columns.
        /// </summary>
        public bool IsColumnContextDisabled { get; set; }

        /// <summary>
        /// Gets or sets whether the raw text of each record should be discarded rather than captured.
        /// </summary>
        /// <remarks>
        /// The reader builds a string of each record's original text so it can be exposed through
        /// <see cref="IRecordContext.Record" />. Nothing in parsing needs it - the column values are
        /// tokenised separately, and a schema selector is handed those values rather than the text -
        /// so for a caller that never reads it, that string is the single largest avoidable cost of
        /// reading a file. Measured over 50,000 records it was a fifth of everything the reader
        /// allocated. Set this to true to skip it, and <see cref="IRecordContext.Record" /> will be
        /// an empty string instead. It defaults to false, so the text is captured unless asked
        /// otherwise.
        /// </remarks>
        public bool IsRecordTextDisabled { get; set; }

        /// <summary>
        /// Gets or sets the global, default format provider.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        /// Duplicates the options.
        /// </summary>
        /// <returns>The new options.</returns>
        public DelimitedOptions Clone()
        {
            return (DelimitedOptions)MemberwiseClone();
        }
    }
}
