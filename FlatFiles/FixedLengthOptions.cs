using System;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Holds configuration settings for the FixedLengthParser class.
    /// </summary>
    public sealed class FixedLengthOptions : IOptions
    {
        /// <summary>
        ///     Initializes a new instance of a FixedLengthParserOptions.
        /// </summary>
        public FixedLengthOptions()
        {
        }
        
        /// <summary>
        ///     Gets or sets the character used to buffer values in a column.
        /// </summary>
        /// <remarks>The fill character can be controlled at the column level using the Window class.</remarks>
        public char FillCharacter { get; set; } = ' ';

        /// <summary>
        ///     Gets or sets whether a separator is present between records.
        /// </summary>
        /// <remarks>
        ///     By default, FlatFiles assumes records are separated by a newline. If set to false,
        ///     FlatFiles will attempt to start reading the next record immediately after the end of
        ///     the previous record.
        /// </remarks>
        public bool HasRecordSeparator { get; set; } = true;

        /// <summary>
        ///     Gets or sets the string that indicates the end of a record.
        /// </summary>
        public string? RecordSeparator { get; set; }

        /// <summary>
        ///     Gets or sets whether the first record in the source holds header information and should be skipped.
        /// </summary>
        public bool IsFirstRecordHeader { get; set; }

        /// <summary>
        ///     Gets whether the first record in the source holds header information and should be skipped.
        /// </summary>
        bool IOptions.IsFirstRecordSchema => IsFirstRecordHeader;

        /// <summary>
        ///     Gets or sets whether a record longer than the total width of the schema's windows is treated as an
        ///     error, as a record shorter than it already is.
        /// </summary>
        /// <remarks>
        ///     By default the characters after the last window are ignored, so a layout declared too narrow reads
        ///     every later column from the wrong offset and reports nothing. Setting this to true raises a
        ///     <see cref="RecordProcessingException" /> for the record instead, with the same record context a short
        ///     record gets, so both can be handled in the same way. It defaults to false because a file whose records
        ///     carry trailing content that was always ignored would otherwise stop reading. It has no effect when
        ///     <see cref="IsRaggedRight" /> is set, because the last column then takes whatever follows the other
        ///     windows and no record is too long.
        /// </remarks>
        public bool IsLongRecordRejected { get; set; }

        /// <summary>
        ///     Gets or sets whether the file is ragged right: every column but the last has a fixed width, and the last
        ///     runs from its offset to the end of the record, so records differ in length.
        /// </summary>
        /// <remarks>
        ///     By default a record shorter than the total width of the schema's windows raises a
        ///     <see cref="RecordProcessingException" /> and a longer one is cut at the last window. With this set to true
        ///     the last column takes everything from its offset to the end of the record, however long or short, and its
        ///     declared width is not used. A record that ends earlier still is read as far as it
        ///     goes: a window the record ends inside takes the characters that are there, and a window the record never
        ///     reaches yields an empty value, which the column's null handling turns into null.
        ///     <see cref="IsLongRecordRejected" /> has no effect. When writing, the last column is written as formatted,
        ///     neither padded nor truncated to its window, so a file read ragged and written ragged keeps its shape.
        /// </remarks>
        public bool IsRaggedRight { get; set; }

        /// <summary>
        ///     Gets or sets the default alignment for the values in the fixed length file.
        /// </summary>
        /// <remarks>The alignment can be controlled at the columnm level using the Window class.</remarks>
        public FixedAlignment Alignment
        {
            get;
            set
            {
                if (!Enum.IsDefined( value ))
                {
                    throw new ArgumentException( Resources.InvalidAlignment, nameof( value ) );
                }
                field = value;
            }
        } = FixedAlignment.LeftAligned;

        /// <summary>
        ///     Gets or sets the default overflow truncation policy to use when a value exceeds the maximum length of its column.
        /// </summary>
        /// <remarks>The trunaction policy can be controlled at the column level using the Window class.</remarks>
        public OverflowTruncationPolicy TruncationPolicy
        {
            get;
            set
            {
                if (!Enum.IsDefined( value ))
                {
                    throw new ArgumentException( Resources.InvalidTruncationPolicy, nameof( value ) );
                }
                field = value;
            }
        } = OverflowTruncationPolicy.TruncateLeading;

        /// <summary>
        ///     Gets or sets whether column-level metadata should be disabled for non-metadata columns.
        /// </summary>
        public bool IsColumnContextDisabled { get; set; }

        /// <summary>
        ///     Gets or sets the global, default format provider.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        ///     Duplicates the options.
        /// </summary>
        /// <returns>The new options.</returns>
        public FixedLengthOptions Clone()
        {
            return (FixedLengthOptions) MemberwiseClone();
        }
    }
}
