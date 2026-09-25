using System;
using System.Collections.Generic;

namespace FlatFiles
{
    /// <summary>
    ///     Caches an execution context per schema, so a reader or writer does not rebuild one for every record.
    /// </summary>
    /// <remarks>
    ///     An execution context pairs a schema with a private copy of the options, and now with a column context
    ///     per column. All of that is fixed for the lifetime of a reader or writer, yet the context - options clone
    ///     included - was being built once per record: two allocations and about 88 bytes a record, a tenth of
    ///     everything a read allocated.
    ///     <para>
    ///         The schema most recently seen is held in a field, which is the whole of the answer for a file of one
    ///         schema and is a reference comparison. A file of several record layouts asks for them in whatever
    ///         order its records come in, so one entry missed on nearly every record; the rest are kept in a map
    ///         beside it, one entry per layout, which a selector or injector bounds. Schemas are compared by
    ///         reference, so this relies on the context types keeping their <c>Schema</c> immutable.
    ///     </para>
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
        private Dictionary<TSchema, TContext>? seen;

        public TContext Get( TSchema? requested )
        {
            var current = context;
            if (current is not null && ReferenceEquals( schema, requested ))
            {
                return current;
            }
            current = requested is null ? create( null ) : Remembered( requested );
            schema = requested;
            context = current;
            return current;
        }

        /// <summary>
        ///     The context for a schema seen before, or a new one remembered against it.
        /// </summary>
        /// <remarks>
        ///     A schema does not override equality, so the map compares by reference as the field above does. What
        ///     it holds is bounded by the layouts a selector or injector was built with.
        /// </remarks>
        private TContext Remembered( TSchema requested )
        {
            seen ??= [];
            if (seen.TryGetValue( requested, out var remembered ))
            {
                return remembered;
            }
            remembered = create( requested );
            seen.Add( requested, remembered );
            return remembered;
        }
    }
}
