using System;

namespace FlatFiles
{
    /// <summary>
    ///     Marks a property or field as a column of a flat file, for a mapping built by
    ///     <c>DefineFromAttributes</c> rather than written out in fluent calls.
    /// </summary>
    /// <remarks>
    ///     A member without this attribute is not mapped. The column's type comes from the member's type, as it does
    ///     for an auto-mapped file, so the attribute carries only what the member cannot say for itself.
    ///     <para>
    ///         Anything beyond a name, an order and a format stays fluent. <c>DefineFromAttributes</c> answers an
    ///         ordinary mapper, so a caller can carry on configuring it - a null formatter, a default value, number
    ///         styles - exactly as they would a mapping written by hand.
    ///     </para>
    /// </remarks>
    [AttributeUsage( AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true )]
    public sealed class ColumnAttribute : Attribute
    {
        /// <summary>
        ///     Initialises a new ColumnAttribute.
        /// </summary>
        public ColumnAttribute()
        {
        }

        /// <summary>
        ///     Initialises a new ColumnAttribute with the name the column has in the file.
        /// </summary>
        /// <param name="name">The name of the column in the file.</param>
        public ColumnAttribute( string name )
        {
            Name = name;
        }

        /// <summary>
        ///     Gets or sets the name the column has in the file. The member's own name is used where this is not set.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        ///     Gets or sets where the column sits in the record, counting from zero.
        /// </summary>
        /// <remarks>
        ///     Reflection does not promise the order it reports members in, so the order columns are written and read
        ///     in is this one. It is required for a fixed-length file, where position is the whole of the mapping, and
        ///     for a delimited file whose first record is not a header. Members that leave it unset keep the order
        ///     reflection gave them, after every member that set it.
        /// </remarks>
        public int Order { get; set; } = Unordered;

        /// <summary>
        ///     Gets or sets the format used to parse and write the value, where its column has one.
        /// </summary>
        /// <remarks>
        ///     Set on both directions where the column reads and writes by a format - a date, a time, a GUID - and on
        ///     the writing direction alone for a number, which parses by its number styles rather than by a format.
        ///     Ignored by a column that has neither, such as text.
        /// </remarks>
        public string? Format { get; set; }

        /// <summary>
        ///     The value <see cref="Order" /> holds when nothing set it.
        /// </summary>
        internal const int Unordered = -1;
    }
}
