using System;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    /// Defines the location and width of a column in a fixed-length record.
    /// </summary>
    public sealed class Window
    {
        /// <summary>
        /// Acts as an indicator that the column or property should capture any data
        /// at the end of a record, past the last window.
        /// </summary>
        public static readonly Window Trailing = new();

        /// <summary>
        /// Initializes a new instance of a Window representing the trailing data in a record.
        /// </summary>
        private Window()
        {
            Width = -1;
        }

        /// <summary>
        /// Initializes a new instance of a Window.
        /// </summary>
        /// <param name="width">The maximum possible width of the column.</param>
        public Window(int width)
        {
            if (width < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, Resources.InvalidColumnWidth);
            }
            Width = width;
        }

        /// <summary>
        /// Gets the width of the column.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets or sets the alignment of the value in the column, using the value found in the FixedLengthOptions object by default.
        /// </summary>
        public FixedAlignment? Alignment
        {
            get => field;
            set
            {
                if (value is not null && !Enum.IsDefined(value.Value))
                {
                    throw new ArgumentException(Resources.InvalidAlignment, nameof(value));
                }
                field = value;
            }
        }

        /// <summary>
        /// Gets or sets the character that is used fill the column.
        /// </summary>
        public char? FillCharacter { get; set; }

        /// <summary>
        /// Gets or sets the truncation policy in case there is an overflow.
        /// A null policy specifies that the default truncation policy of the writer will be used.
        /// </summary>
        public OverflowTruncationPolicy? TruncationPolicy 
        {
            get => field;
            set
            {
                if (value is not null && !Enum.IsDefined(value.Value))
                {
                    throw new ArgumentException(Resources.InvalidTruncationPolicy, nameof(value));
                }
                field = value;
            }
        }

        /// <summary>
        /// Implicitly creates a window from a width.
        /// </summary>
        /// <param name="width">The width to create a window for.</param>
        /// <returns>The new window.</returns>
        public static implicit operator Window(int width)
        {
            return new Window(width);
        }
    }
}
