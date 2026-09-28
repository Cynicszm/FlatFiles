namespace FlatFiles
{
    /// <summary>
    ///     What the three execution contexts share: one column context per column, reused from record to record.
    /// </summary>
    /// <remarks>
    ///     A column that consults the options' format provider can only reach it through a column context, so one
    ///     has to exist for every such column of every record - 40 bytes apiece, which on a wide file is most of
    ///     what reading or writing a record allocates. Nothing sees these: they are given only to a library column
    ///     with no hook attached, which reads the provider off and lets go. Anything that reaches user code - a
    ///     hook, a metadata or complex column, a column declared elsewhere, or an error being recovered from - is
    ///     given a context of its own, built fresh, because a handler may keep hold of it.
    ///     <para>
    ///         An execution context belongs to one reader or writer and to one schema, so the column definition and
    ///         physical index a cached context was built with stay true for as long as it lives. Like the readers
    ///         and writers themselves, this is not safe for concurrent use.
    ///     </para>
    /// </remarks>
    internal abstract class ExecutionContextBase
    {
        private ColumnContext?[]? columnContexts;

        /// <summary>
        ///     Where in the record each column's value sits, one entry per column of the schema, or null when the
        ///     values are simply in the order the columns are. A metadata column, which takes nothing from the
        ///     record, and a column the header did not carry both hold -1.
        /// </summary>
        /// <remarks>
        ///     Built once, from the header, by a reader asked to match columns by name. It lives here rather than on
        ///     the schema because a schema is shared between readers, writers and files, and the order one file
        ///     happens to be in is not a property of the schema.
        /// </remarks>
        public int[]? SourceMap { get; set; }

        /// <summary>
        ///     Gets the context for the given column, pointed at the record being processed.
        /// </summary>
        /// <param name="recordContext">The record being read or written.</param>
        /// <param name="columnCount">How many columns the schema declares.</param>
        /// <param name="physicalIndex">The column's position in the schema.</param>
        /// <param name="logicalIndex">The column's position among those that carry a value, or -1.</param>
        public ColumnContext GetColumnContext( IRecordContext recordContext, int columnCount, int physicalIndex, int logicalIndex )
        {
            var contexts = columnContexts ??= new ColumnContext?[columnCount];
            var cached = contexts[physicalIndex];
            if (cached is null)
            {
                cached = new ColumnContext( recordContext, physicalIndex, logicalIndex );
                contexts[physicalIndex] = cached;
                return cached;
            }
            cached.Reuse( recordContext, logicalIndex );
            return cached;
        }
    }
}
