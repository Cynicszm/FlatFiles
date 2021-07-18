﻿using System;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    /// Holds configuration settings for the FixedLengthParser class.
    /// </summary>
    public sealed class FixedLengthOptions : IOptions
    {
        private FixedAlignment alignment = FixedAlignment.LeftAligned;
        private OverflowTruncationPolicy truncationPolicy = OverflowTruncationPolicy.TruncateLeading;

        /// <summary>
        /// Initializes a new instance of a FixedLengthParserOptions.
        /// </summary>
        public FixedLengthOptions()
        {
        }
        
        /// <summary>
        /// Gets or sets the character used to buffer values in a column.
        /// </summary>
        /// <remarks>The fill character can be controlled at the column level using the Window class.</remarks>
        public char FillCharacter { get; set; } = ' ';

        /// <summary>
        /// Gets or sets whether a separator is present between records.
        /// </summary>
        /// <remarks>
        /// By default, FlatFiles assumes records are separated by a newline. If set to false,
        /// FlatFiles will attempt to start reading the next record immediately after the end of
        /// the previous record.
        /// </remarks>
        public bool HasRecordSeparator { get; set; } = true;

        /// <summary>
        /// Gets or sets the string that indicates the end of a record.
        /// </summary>
        public string? RecordSeparator { get; set; }

        /// <summary>
        /// Gets or sets whether the first record in the source holds header information and should be skipped.
        /// </summary>
        public bool IsFirstRecordHeader { get; set; }

        /// <summary>
        /// Gets whether the first record in the source holds header information and should be skipped.
        /// </summary>
        bool IOptions.IsFirstRecordSchema => IsFirstRecordHeader;

        /// <summary>
        /// Gets or sets the default alignment for the values in the fixed length file.
        /// </summary>
        /// <remarks>The alignment can be controlled at the columnm level using the Window class.</remarks>
        public FixedAlignment Alignment
        {
            get => alignment;
            set
            {
                if (!Enum.IsDefined(typeof(FixedAlignment), value))
                {
                    throw new ArgumentException(Resources.InvalidAlignment, nameof(value));
                }
                alignment = value;
            }
        }

        /// <summary>
        /// Gets or sets the default overflow truncation policy to use when a value exceeds the maximum length of its column.
        /// </summary>
        /// <remarks>The trunaction policy can be controlled at the column level using the Window class.</remarks>
        public OverflowTruncationPolicy TruncationPolicy
        {
            get => truncationPolicy;
            set
            {
                if (!Enum.IsDefined(typeof(OverflowTruncationPolicy), value))
                {
                    throw new ArgumentException(Resources.InvalidTruncationPolicy, nameof(value));
                }
                truncationPolicy = value;
            }
        }

        /// <summary>
        /// Gets or sets whether column-level metadata should be disabled for non-metadata columns.
        /// </summary>
        public bool IsColumnContextDisabled { get; set; }

        /// <summary>
        /// Gets or sets the global, default format provider.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        /// Duplicates the options.
        /// </summary>
        /// <returns>The new options.</returns>
        public FixedLengthOptions Clone()
        {
            return (FixedLengthOptions)MemberwiseClone();
        }
    }
}
