using System;

namespace FlatFiles
{
    /// <summary>
    ///     A stretch of a fixed-length record that no member maps to: filler, a field the class does not care
    ///     about, or one it is not allowed to read.
    /// </summary>
    /// <remarks>
    ///     Declared on the class rather than on a member, because the whole point of it is that there is no member
    ///     to hang it on. It takes its place among the columns by <see cref="Order" />, exactly as a mapped member
    ///     does, and the record is written with the width reserved and nothing put in it.
    ///     <para>
    ///         Ignored by a delimited mapping, which has no fixed positions to leave gaps in.
    ///     </para>
    /// </remarks>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true )]
    public sealed class IgnoredWindowAttribute : Attribute
    {
        /// <summary>
        ///     Initialises a new IgnoredWindowAttribute.
        /// </summary>
        /// <param name="order">Where the stretch sits among the columns, counting from zero.</param>
        /// <param name="width">How many characters it occupies.</param>
        public IgnoredWindowAttribute( int order, int width )
        {
            Order = order;
            Width = width;
        }

        /// <summary>
        ///     Gets where the stretch sits among the columns, counting from zero.
        /// </summary>
        public int Order { get; }

        /// <summary>
        ///     Gets how many characters it occupies.
        /// </summary>
        public int Width { get; }
    }
}
