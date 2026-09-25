using FlatFiles.Properties;

namespace FlatFiles
{
    internal sealed class ColumnContext : IColumnContext
    {
        public ColumnContext(IRecordContext recordContext, int physicalIndex, int logicalIndex)
        {
            var schema = recordContext.ExecutionContext.Schema;
            if (schema is null)
            {
                throw new FlatFileException(Resources.SchemaNotDefined);
            }
            RecordContext = recordContext;
            ColumnDefinition = schema.ColumnDefinitions[physicalIndex];
            PhysicalIndex = physicalIndex;
            LogicalIndex = logicalIndex;
        }

        public IRecordContext RecordContext { get; private set; }

        public IColumnDefinition ColumnDefinition { get; }

        public int PhysicalIndex { get; }

        public int LogicalIndex { get; private set; }

        /// <summary>
        ///     Points this context at another record, so that one instance can serve a column for a whole file.
        /// </summary>
        /// <param name="recordContext">The record now being read or written.</param>
        /// <param name="logicalIndex">The column's position among those that carry a value, or -1.</param>
        /// <remarks>
        ///     The column definition and physical index do not move: an execution context belongs to one schema.
        ///     Only a context that user code cannot reach is reused - see <see cref="ExecutionContextBase" />.
        /// </remarks>
        internal void Reuse( IRecordContext recordContext, int logicalIndex )
        {
            RecordContext = recordContext;
            LogicalIndex = logicalIndex;
        }
    }
}
