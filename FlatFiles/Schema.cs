using System;
using FlatFiles.TypeMapping;

namespace FlatFiles
{
    /// <summary>
    ///     Defines the expected format of a record in a file.
    /// </summary>
    public abstract class Schema : ISchema
    {
        /// <summary>
        ///     Initialises a new instance of a Schema.
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
        /// <param name="parsedValues">The array to put the parsed objects in, one slot per physical column.</param>
        /// <returns>The parsed objects.</returns>
        internal object?[] ParseValues( IRecoverableRecordContext context, string[] values, object?[] parsedValues )
        {
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
        /// <param name="parsedValues">The array to put the parsed objects in, one slot per physical column.</param>
        /// <returns>The parsed objects.</returns>
        internal object?[] ParseValues( IRecoverableRecordContext context, RawRecord values, object?[] parsedValues )
        {
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

        /// <summary>
        ///     Parses the record straight onto an entity, one column at a time, so that no value is ever an
        ///     <see cref="object" />. A type mapper supplies one setter per column, in the order the columns are
        ///     read, and takes this path only when every column and member it maps allows it.
        /// </summary>
        /// <typeparam name="TEntity">The type being read into.</typeparam>
        /// <param name="context">The metadata for the current record being processed.</param>
        /// <param name="values">The raw values of the record.</param>
        /// <param name="entity">The entity to read into.</param>
        /// <param name="setters">One setter per column that yields a value, in order.</param>
        internal void ParseValues<TEntity>( IRecoverableRecordContext context, RawRecord values, TEntity entity, IColumnSetter<TEntity>[] setters )
        {
            for (int columnIndex = 0, sourceIndex = 0, destinationIndex = 0, columnCount = ColumnDefinitions.Count;
                columnIndex != columnCount;
                ++columnIndex)
            {
                var definition = ColumnDefinitions[columnIndex];
                if (definition is IMetadataColumn)
                {
                    // Nothing in the record feeds this column, so it takes a destination but no source.
                    var columnContext = NewColumnContext( context, columnIndex, destinationIndex );
                    setters[destinationIndex].SetObject( entity, ParseWithContext( columnContext, string.Empty ) );
                    ++destinationIndex;
                    continue;
                }
                if (definition.IsIgnored)
                {
                    // An ignored column still runs whatever is attached to it, and still discards the result.
                    ParseValue( context, columnIndex, -1, values[sourceIndex] );
                    ++sourceIndex;
                    continue;
                }
                ParseValueInto( context, columnIndex, destinationIndex, values[sourceIndex], entity, setters[destinationIndex] );
                ++sourceIndex;
                ++destinationIndex;
            }
        }

        private void ParseValueInto<TEntity>( IRecoverableRecordContext context, int columnIndex, int destinationIndex, ReadOnlySpan<char> rawValue, TEntity entity, IColumnSetter<TEntity> setter )
        {
            var options = context.ExecutionContext.Options;
            var definition = ColumnDefinitions[columnIndex];
            if (options.IsColumnContextDisabled)
            {
                try
                {
                    setter.Set( null, entity, rawValue );
                }
                catch (Exception exception)
                {
                    throw new ColumnProcessingException( definition, destinationIndex, rawValue.ToString(), exception );
                }
                return;
            }
            if (definition.IsColumnContextRequired || options.FormatProvider is not null)
            {
                var columnContext = NewColumnContext( context, columnIndex, destinationIndex );
                try
                {
                    setter.Set( columnContext, entity, rawValue );
                }
                catch (Exception exception)
                {
                    // A handler's substitution arrives as an object, which is the one value on this path that boxes.
                    setter.SetObject( entity, RecoverParse( columnContext, rawValue.ToString(), exception ) );
                }
                return;
            }
            try
            {
                setter.Set( null, entity, rawValue );
            }
            catch (Exception exception)
            {
                setter.SetObject( entity, RecoverParse( NewColumnContext( context, columnIndex, destinationIndex ), rawValue.ToString(), exception ) );
            }
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

        /// <summary>
        ///     Formats a record straight from the entity it is written from, asking each column's getter for its
        ///     member rather than being handed an array of values that were boxed to fill it.
        /// </summary>
        /// <typeparam name="TEntity">The type being written from.</typeparam>
        /// <param name="context">The metadata for the record currently being processed.</param>
        /// <param name="entity">The entity being written.</param>
        /// <param name="getters">One per logical column, in the order the schema declares them.</param>
        /// <param name="destination">The buffer that receives the formatted record.</param>
        /// <param name="handler">Receives a callback before and after each column is formatted.</param>
        internal void FormatValues<TEntity>( IRecoverableRecordContext context, TEntity entity, IColumnGetter<TEntity>[] getters, RecordBuffer destination, IFormattedColumnHandler handler )
        {
            var definitions = ColumnDefinitions;
            for (int columnIndex = 0, valueIndex = 0, columnCount = definitions.Count; columnIndex != columnCount; ++columnIndex)
            {
                var definition = definitions[columnIndex];
                handler.ColumnStarting( columnIndex, destination );
                var start = destination.Length;
                if (definition is IMetadataColumn)
                {
                    // Its value comes from the context rather than from the entity, as when parsing.
                    FormatWithContext( NewColumnContext( context, columnIndex, valueIndex ), null, destination );
                    ++valueIndex;
                }
                else if (!definition.IsIgnored)
                {
                    FormatMember( context, columnIndex, valueIndex, entity, getters[valueIndex], destination );
                    ++valueIndex;
                }
                else
                {
                    FormatValue( context, columnIndex, -1, null, destination );
                }
                handler.ColumnFormatted( columnIndex, start, destination );
            }
        }

        private void FormatMember<TEntity>( IRecoverableRecordContext context, int columnIndex, int valueIndex, TEntity entity, IColumnGetter<TEntity> getter, RecordBuffer destination )
        {
            var options = context.ExecutionContext.Options;
            var definition = ColumnDefinitions[columnIndex];
            // As when parsing: no context unless the column asks for one, or something fails and the error needs
            // one to describe itself.
            var needsContext = !options.IsColumnContextDisabled && ( definition.IsColumnContextRequired || options.FormatProvider is not null );
            var columnContext = needsContext ? NewColumnContext( context, columnIndex, valueIndex ) : null;
            var start = destination.Length;
            try
            {
                getter.Write( columnContext, entity, destination );
            }
            catch (Exception exception)
            {
                // Whatever the column managed to write before it failed must not leak into the record.
                destination.Truncate( start );
                if (options.IsColumnContextDisabled)
                {
                    throw new ColumnProcessingException( definition, valueIndex, getter.Read( entity ), exception );
                }
                RecoverFormat( columnContext ?? NewColumnContext( context, columnIndex, valueIndex ), getter.Read( entity ), destination, exception );
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
