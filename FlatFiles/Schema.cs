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
            var parsedValues = new object?[ColumnDefinitions.PhysicalCount];
            for (int columnIndex = 0, sourceIndex = 0, destinationIndex = 0, columnCount = ColumnDefinitions.Count;
                columnIndex != columnCount;
                ++columnIndex)
            {
                var definition = ColumnDefinitions[columnIndex];
                if (definition is IMetadataColumn)
                {
                    var columnContext = NewColumnContext( context, columnIndex, destinationIndex );
                    var metadata = ParseWithContext( columnContext, string.Empty );
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

        /// <summary>
        ///     Parses the values sitting at the given positions within the record, assuming that they are in the same
        ///     order as the column definitions. Nothing is copied out of the record for a column that can read its
        ///     value from the characters themselves.
        /// </summary>
        /// <param name="context">The metadata for the current record being processed.</param>
        /// <param name="values">The raw values of the record, as ranges within the text they were read from.</param>
        /// <returns>The parsed objects.</returns>
        internal object?[] ParseValues( IRecoverableRecordContext context, RawRecord values )
        {
            var parsedValues = new object?[ColumnDefinitions.PhysicalCount];
            for (int columnIndex = 0, sourceIndex = 0, destinationIndex = 0, columnCount = ColumnDefinitions.Count;
                columnIndex != columnCount;
                ++columnIndex)
            {
                var definition = ColumnDefinitions[columnIndex];
                if (definition is IMetadataColumn)
                {
                    var columnContext = NewColumnContext( context, columnIndex, destinationIndex );
                    var metadata = ParseWithContext( columnContext, string.Empty );
                    parsedValues[destinationIndex] = metadata;
                    ++destinationIndex;
                }
                else if (!definition.IsIgnored)
                {
                    var parsedValue = ParseValue( context, columnIndex, destinationIndex, values[sourceIndex] );
                    parsedValues[destinationIndex] = parsedValue;
                    ++sourceIndex;
                    ++destinationIndex;
                }
                else
                {
                    ParseValue( context, columnIndex, -1, values[sourceIndex] );
                    ++sourceIndex;
                }
            }
            return parsedValues;
        }

        private object? ParseValue( IRecoverableRecordContext context, int columnIndex, int destinationIndex, ReadOnlySpan<char> rawValue )
        {
            var options = context.ExecutionContext.Options;
            var definition = ColumnDefinitions[columnIndex];
            if (options.IsColumnContextDisabled)
            {
                try
                {
                    return definition.Parse( null, rawValue );
                }
                catch (Exception exception)
                {
                    throw new ColumnProcessingException( definition, destinationIndex, rawValue.ToString(), exception );
                }
            }
            if (definition.IsColumnContextRequired || options.FormatProvider is not null)
            {
                // The options' format provider reaches a column only through the context, so its presence keeps it.
                var columnContext = NewColumnContext( context, columnIndex, destinationIndex );
                try
                {
                    return definition.Parse( columnContext, rawValue );
                }
                catch (Exception exception)
                {
                    // The value is only copied here, where the error carries it to the handler.
                    return RecoverParse( columnContext, rawValue.ToString(), exception );
                }
            }
            // Nothing on this column can look at its context, so none is built unless the parse fails and the error
            // has to be reported with one.
            try
            {
                return definition.Parse( null, rawValue );
            }
            catch (Exception exception)
            {
                return RecoverParse( NewColumnContext( context, columnIndex, destinationIndex ), rawValue.ToString(), exception );
            }
        }

        private object? ParseValue( IRecoverableRecordContext context, int columnIndex, int destinationIndex, string rawValue )
        {
            var options = context.ExecutionContext.Options;
            var definition = ColumnDefinitions[columnIndex];
            if (options.IsColumnContextDisabled)
            {
                return ParseWithoutContext( definition, destinationIndex, rawValue );
            }
            if (definition.IsColumnContextRequired || options.FormatProvider is not null)
            {
                // The options' format provider reaches a column only through the context, so its presence keeps it.
                return ParseWithContext( NewColumnContext( context, columnIndex, destinationIndex ), rawValue );
            }
            // Nothing on this column can look at its context, so none is built unless the parse fails and the error
            // has to be reported with one.
            try
            {
                return definition.Parse( null, rawValue );
            }
            catch (Exception exception)
            {
                return RecoverParse( NewColumnContext( context, columnIndex, destinationIndex ), rawValue, exception );
            }
        }

        private object? ParseWithContext( IColumnContext columnContext, string rawValue )
        {
            try
            {
                return columnContext.ColumnDefinition.Parse( columnContext, rawValue );
            }
            catch (Exception exception)
            {
                return RecoverParse( columnContext, rawValue, exception );
            }
        }

        private object? RecoverParse( IColumnContext columnContext, string rawValue, Exception exception )
        {
            var columnException = new ColumnProcessingException( columnContext, rawValue, exception );
            if (columnContext.RecordContext is not IRecoverableRecordContext { HasHandler: true } recordContext)
            {
                throw columnException;
            }
            var e = new ColumnErrorEventArgs( columnException );
            recordContext.ProcessError( this, e );
            return !e.IsHandled ? throw columnException : e.Substitution;
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
            var options = context.ExecutionContext.Options;
            var definition = ColumnDefinitions[columnIndex];
            if (options.IsColumnContextDisabled)
            {
                FormatWithoutContext( definition, valueIndex, value, destination );
                return;
            }
            if (!definition.IsColumnContextRequired && options.FormatProvider is null)
            {
                // As when parsing: no context unless the column fails and the error needs one.
                var start = destination.Length;
                try
                {
                    definition.Format( null, value, destination );
                }
                catch (Exception exception)
                {
                    destination.Truncate( start );
                    RecoverFormat( NewColumnContext( context, columnIndex, valueIndex ), value, destination, exception );
                }
                return;
            }
            FormatWithContext( NewColumnContext( context, columnIndex, valueIndex ), value, destination );
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
                RecoverFormat( columnContext, value, destination, exception );
            }
        }

        private void RecoverFormat( IColumnContext columnContext, object? value, RecordBuffer destination, Exception exception )
        {
            var columnException = new ColumnProcessingException( columnContext, value, exception );
            if (columnContext.RecordContext is not IRecoverableRecordContext { HasHandler: true } recordContext)
            {
                throw columnException;
            }
            var e = new ColumnErrorEventArgs( columnException );
            recordContext.ProcessError( this, e );
            if (!e.IsHandled)
            {
                throw columnException;
            }
            destination.Write( ((string?) e.Substitution ?? string.Empty).AsSpan() );
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
