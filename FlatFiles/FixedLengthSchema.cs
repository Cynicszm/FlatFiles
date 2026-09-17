using System;
using System.Collections.Generic;

namespace FlatFiles
{
    /// <summary>
    ///     Defines the expected format of a fixed-length file record.
    /// </summary>
    public sealed class FixedLengthSchema : Schema
    {
        private readonly List<Window> windows = [];
        private ColumnCollection? cachedColumns;
        private IColumnDefinition? trailing;

        /// <summary>
        ///     Initializes a new instance of a FixedLengthSchema.
        /// </summary>
        public FixedLengthSchema()
        {
        }
        
        /// <summary>
        ///     Adds a column to the schema, using the given definition to define it.
        /// </summary>
        /// <param name="definition">The definition of the column to add.</param>
        /// <param name="window">Describes the column</param>
        /// <returns>The current schema.</returns>
        public FixedLengthSchema AddColumn( IColumnDefinition definition, Window window )
        {
            ArgumentNullException.ThrowIfNull( window );
            if (window == Window.Trailing)
            {
                trailing = definition;
            }
            else
            {
                AddColumnBase( definition );
                windows.Add( window );
                if (definition is not IMetadataColumn)
                {
                    TotalWidth += window.Width;
                }
            }
            cachedColumns = null;
            return this;
        }

        /// <inheritdoc />
        public override ColumnCollection ColumnDefinitions
        {
            get
            {
                if (trailing is null)
                {
                    return base.ColumnDefinitions;
                }
                if (cachedColumns is not null)
                {
                    return cachedColumns;
                }
                var copy = new ColumnCollection( base.ColumnDefinitions );
                copy.AddColumn( trailing );
                cachedColumns = copy;
                return copy;
            }
        }

        /// <summary>
        ///     Gets the column widths.
        /// </summary>
        public WindowCollection Windows => new( windows );

        /// <summary>
        ///     Gets the total width of all columns.
        /// </summary>
        internal int TotalWidth { get; private set; }
    }
}
