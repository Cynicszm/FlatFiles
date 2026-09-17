using System;

namespace FlatFiles
{
    /// <summary>
    ///     Caches the execution context for the schema most recently seen, so a reader or writer does not
    ///     rebuild it for every record.
    /// </summary>
    /// <remarks>
    ///     An execution context pairs a schema with a private copy of the options. Both are fixed for the
    ///     lifetime of a reader or writer, yet the context - options clone included - was being built once
    ///     per record: two allocations and about 88 bytes a record, a tenth of everything a read allocated.
    ///     One entry is enough. A fixed schema hits on every record; a schema selector misses only when
    ///     consecutive records take different schemas; and a schema inferred from a record's width is
    ///     shared for equal widths, so it misses only when the width changes. Schemas are compared by
    ///     reference, so this relies on the context types keeping their <c>Schema</c> immutable.
    ///     The record context that wraps the execution context is deliberately not cached: it is mutable and
    ///     reaches user code through the reader and writer events, where a handler may keep hold of it.
    ///     Like the readers and writers themselves, this is not safe for concurrent use.
    /// </remarks>
    internal sealed class ExecutionContextCache<TSchema, TContext>( Func<TSchema?, TContext> create )
        where TSchema : class
        where TContext : class
    {
        private TSchema? schema;
        private TContext? context;

        public TContext Get( TSchema? requested )
        {
            var current = context;
            if (current is not null && ReferenceEquals( schema, requested ))
            {
                return current;
            }
            current = create( requested );
            schema = requested;
            context = current;
            return current;
        }
    }
}
