using System;

namespace FlatFiles
{
    /// <summary>
    ///     Defines the expected format of a record in a file.
    /// </summary>
    public abstract class Schema : ISchema
    {
        /// <summary>
        ///     Initializes a new instance of a Schema.
        /// </summary>
        protected Schema()
        {
        }

        /// <summary>
        ///     Gets the column definitions that make up the schema.
        /// </summary>
        public virtual ColumnCollection ColumnDefinitions { get; } = new();

        /// <summary>
        ///     Gets the index of the column with the given name.
        /// </summary>
        /// <param name="columnName">The name of the column to get the index for.</param>
        /// <returns>The index of the column with the given name -or- -1 if the name is not found.</returns>
        public int GetOrdinal( string columnName )
        {
            return ColumnDefinitions.GetOrdinal( columnName );
        }

        /// <summary>
        ///     Adds a column to the schema, using the given definition to define it.
        /// </summary>
        /// <param name="definition">The definition of the column to add.</param>
        /// <returns>The current schema.</returns>
        protected void AddColumnBase( IColumnDefinition definition )
        {
            ColumnDefinitions.AddColumn( definition );
        }

        /// <summary>
        ///     Parses the given values assuming that they are in the same order as the column definitions.
        /// </summary>
        /// <param name="context">The metadata for the current record being processed.</param>
        /// <param name="values">The values to parse.</param>
        /// <returns>The parsed objects.</returns>
        internal object?[] ParseValues( IRecoverableRecordContext context, string[] values )
        {
            object?[] parsedValues = new object?[ColumnDefinitions.PhysicalCount];
            for (int columnIndex = 0, sourceIndex = 0, destinationIndex = 0, columnCount = ColumnDefinitions.Count; 
                columnIndex != columnCount; 
                ++columnIndex)
            {
                var definition = ColumnDefinitions[columnIndex];
                if (definition is IMetadataColumn)
                {
                    var columnContext = NewColumnContext( context, columnIndex, destinationIndex );
                    var metadata = ParseWithContext( columnContext, String.Empty );
                    parsedValues[destinationIndex] = metadata;
                    ++destinationIndex;
                }
                else if (!definition.IsIgnored)
                {
                    var rawValue = values[sourceIndex];
                    var parsedValue = ParseValue( context, columnIndex, destinationIndex, rawValue );
                    parsedValues[destinationIndex] = parsedValue;
                    ++sourceIndex;
                    ++destinationIndex;
                }
                else
                {
                    var rawValue = values[sourceIndex];
                    ParseValue( context, columnIndex, -1, rawValue );
                    ++sourceIndex;
                }
            }
            return parsedValues;
        }

        private object? ParseValue( IRecoverableRecordContext context, int columnIndex, int destinationIndex, string rawValue )
        {
            var isContextDisabled = context.ExecutionContext.Options.IsColumnContextDisabled;
            if (isContextDisabled)
            {
                var definition = ColumnDefinitions[columnIndex];
                return ParseWithoutContext( definition, destinationIndex, rawValue );
            }
            var columnContext = NewColumnContext( context, columnIndex, destinationIndex );
            return ParseWithContext( columnContext, rawValue );
        }

        private object? ParseWithContext( IColumnContext columnContext, string rawValue )
        {
            try
            {
                var definition = columnContext.ColumnDefinition;
                var parsedValue = definition.Parse( columnContext, rawValue );
                return parsedValue;
            }
            catch (Exception exception)
            {
                var columnException = new ColumnProcessingException( columnContext, rawValue, exception );
                if (columnContext.RecordContext is IRecoverableRecordContext { HasHandler: true } recordContext)
                {
                    var e = new ColumnErrorEventArgs( columnException );
                    recordContext.ProcessError( this, e );
                    if (e.IsHandled)
                    {
                        return e.Substitution;
                    }
                }
                throw columnException;
            }
        }

        private static object? ParseWithoutContext( IColumnDefinition definition, int position, string rawValue )
        {
            try
            {
                var parsedValue = definition.Parse( null, rawValue );
                return parsedValue;
            }
            catch (Exception exception)
            {
                throw new ColumnProcessingException( definition, position, rawValue, exception );
            }
        }

        /// <summary>
        ///     Formats the given values into the buffer, assuming they are in the same order as the column
        ///     definitions, and lets the handler step in before and after each column so it can add separators and
        ///     edit the column's text in place.
        /// </summary>
        /// <param name="context">The metadata for the record currently being processed.</param>
        /// <param name="values">The values to format.</param>
        /// <param name="destination">The buffer that receives the formatted record.</param>
        /// <param name="handler">Receives a callback before and after each column is formatted.</param>
        internal void FormatValues( IRecoverableRecordContext context, object?[] values, RecordBuffer destination, IFormattedColumnHandler handler )
        {
            var definitions = ColumnDefinitions;
            for (int columnIndex = 0, valueIndex = 0, columnCount = definitions.Count; columnIndex != columnCount; ++columnIndex)
            {
                var definition = definitions[columnIndex];
                handler.ColumnStarting( columnIndex, destination );
                var start = destination.Length;
                if (definition is IMetadataColumn)
                {
                    var columnContext = NewColumnContext( context, columnIndex, valueIndex );
                    FormatWithContext( columnContext, null, destination );
                    ++valueIndex;
                }
                else if (!definition.IsIgnored)
                {
                    FormatValue( context, columnIndex, valueIndex, values[valueIndex], destination );
                    ++valueIndex;
                }
                else
                {
                    FormatValue( context, columnIndex, -1, null, destination );
                }
                handler.ColumnFormatted( columnIndex, start, destination );
            }
        }

        private void FormatValue( IRecoverableRecordContext context, int columnIndex, int valueIndex, object? value, RecordBuffer destination )
        {
            if (context.ExecutionContext.Options.IsColumnContextDisabled)
            {
                FormatWithoutContext( ColumnDefinitions[columnIndex], valueIndex, value, destination );
            }
            else
            {
                var columnContext = NewColumnContext( context, columnIndex, valueIndex );
                FormatWithContext( columnContext, value, destination );
            }
        }

        private void FormatWithContext( IColumnContext columnContext, object? value, RecordBuffer destination )
        {
            var start = destination.Length;
            try
            {
                columnContext.ColumnDefinition.Format( columnContext, value, destination );
            }
            catch (Exception exception)
            {
                // Whatever the column managed to write before it failed must not leak into the record.
                destination.Truncate( start );
                var columnException = new ColumnProcessingException( columnContext, value, exception );
                if (columnContext.RecordContext is IRecoverableRecordContext { HasHandler: true } recordContext)
                {
                    var e = new ColumnErrorEventArgs( columnException );
                    recordContext.ProcessError( this, e );
                    if (e.IsHandled)
                    {
                        destination.Write( ((string?) e.Substitution ?? String.Empty).AsSpan() );
                        return;
                    }
                }
                throw columnException;
            }
        }

        private static void FormatWithoutContext( IColumnDefinition definition, int position, object? value, RecordBuffer destination )
        {
            try
            {
                definition.Format( null, value, destination );
            }
            catch (Exception exception)
            {
                throw new ColumnProcessingException( definition, position, value, exception );
            }
        }

        private static ColumnContext NewColumnContext( IRecordContext context, int physicalIndex, int logicalIndex )
        {
            return new ColumnContext( context, physicalIndex, logicalIndex );
        }
    }
}
