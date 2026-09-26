using System;

namespace FlatFiles
{
    /// <summary>
    /// Represents reader/writer options that are common among file types.
    /// </summary>
    public interface IOptions
    {
        /// <summary>
        /// Gets whether the first record defines the schema of the file.
        /// </summary>
        bool IsFirstRecordSchema { get; }

        /// <summary>
        /// Gets whether column-level metadata should be disabled for non-metadata columns.
        /// </summary>
        bool IsColumnContextDisabled { get; }

        /// <summary>
        /// Gets the global, default format provider to use.
        /// </summary>
        IFormatProvider? FormatProvider { get; }

        /// <summary>
        /// Gets the text a record must start with to be passed over as a comment, or null to read every record.
        /// </summary>
        /// <remarks>
        /// Defaulted so that an implementation written before this existed keeps compiling and keeps its behaviour.
        /// </remarks>
        string? CommentPrefix => null;

        /// <summary>
        /// Gets whether a record that is empty, or holds nothing but whitespace, is passed over.
        /// </summary>
        /// <remarks>
        /// Defaulted so that an implementation written before this existed keeps compiling and keeps its behaviour.
        /// </remarks>
        bool IsBlankRecordSkipped => false;
    }
}
