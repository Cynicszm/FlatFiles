using System;

namespace FlatFiles
{
    /// <summary>
    ///     Gives a column its window in a fixed-length file: how wide it is, and what to do with a value that does
    ///     not fill it or does not fit it.
    /// </summary>
    /// <remarks>
    ///     Required on every member a fixed-length mapping takes, and ignored by a delimited one. It is the reason
    ///     these attributes exist: a width belongs beside the member it describes, and a fixed-length mapping written
    ///     in fluent calls says it somewhere else.
    /// </remarks>
    [AttributeUsage( AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true )]
    public sealed class WindowAttribute : Attribute
    {
        /// <summary>
        ///     Initialises a new WindowAttribute of the given width.
        /// </summary>
        /// <param name="width">How many characters the column occupies.</param>
        public WindowAttribute( int width )
        {
            Width = width;
        }

        /// <summary>
        ///     Gets how many characters the column occupies.
        /// </summary>
        public int Width { get; }

        /// <summary>
        ///     Gets or sets which side of its window a value sits on. The options' default is used where this is
        ///     not set.
        /// </summary>
        public FixedAlignment Alignment
        {
            get => alignment ?? FixedAlignment.LeftAligned;
            set => alignment = value;
        }

        private FixedAlignment? alignment;

        /// <summary>
        ///     Gets or sets the character a value is padded with. The options' default is used where this is not set.
        /// </summary>
        public char FillCharacter
        {
            get => fillCharacter ?? ' ';
            set => fillCharacter = value;
        }

        private char? fillCharacter;

        /// <summary>
        ///     Gets or sets which end of a value too wide for its window is cut off. The options' default is used
        ///     where this is not set.
        /// </summary>
        public OverflowTruncationPolicy TruncationPolicy
        {
            get => truncationPolicy ?? OverflowTruncationPolicy.TruncateLeading;
            set => truncationPolicy = value;
        }

        private OverflowTruncationPolicy? truncationPolicy;

        /// <summary>
        ///     The window this attribute describes, with anything it did not set left for the options to decide.
        /// </summary>
        internal Window ToWindow()
        {
            var window = new Window( Width );
            if (alignment is not null)
            {
                window.Alignment = alignment;
            }
            if (fillCharacter is not null)
            {
                window.FillCharacter = fillCharacter;
            }
            if (truncationPolicy is not null)
            {
                window.TruncationPolicy = truncationPolicy;
            }
            return window;
        }
    }
}
