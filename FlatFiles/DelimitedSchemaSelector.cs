using System;
using System.Collections.Generic;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a class that can dynamically provide the schema based on the shape of a read record.
    /// </summary>
    public sealed class DelimitedSchemaSelector
    {
        private static readonly SchemaMatcher NonMatcher = new( null, _ => false );
        private readonly List<SchemaMatcher> matchers = [];
        private SchemaMatcher defaultMatcher = NonMatcher;

        /// <summary>
        ///     Initializes a new instance of a DelimitedSchemaSelector.
        /// </summary>
        public DelimitedSchemaSelector()
        {
        }

        /// <summary>
        ///     Indicates that the given schema should be used when the predicate returns true.
        /// </summary>
        /// <param name="predicate">Indicates whether the schema should be used for a record.</param>
        /// <returns>An object for specifying which schema to use when the predicate matches.</returns>
        /// <exception cref="ArgumentNullException">The predicate is null.</exception>
        /// <remarks>Previously registered schemas will be used if their predicates match.</remarks>
        public IDelimitedSchemaSelectorWhenBuilder When( Func<string[], bool> predicate )
        {
            ArgumentNullException.ThrowIfNull( predicate );
            return new DelimitedSchemaSelectorWhenBuilder( this, predicate );
        }

        /// <summary>
        ///     Provides the schema to use by default when no other matches are found.
        /// </summary>
        /// <param name="schema">The default schema to use.</param>
        /// <returns>The current selector to allow for further customization.</returns>
        public IDelimitedSchemaSelectorUseBuilder WithDefault( DelimitedSchema? schema )
        {
            defaultMatcher = schema is null ? NonMatcher : new SchemaMatcher( schema, _ => true );
            return new DelimitedSchemaSelectorUseBuilder( defaultMatcher );
        }

        private SchemaMatcher Add( DelimitedSchema schema, Func<string[], bool> predicate )
        {
            var matcher = new SchemaMatcher( schema, predicate );
            matchers.Add( matcher );
            return matcher;
        }

        internal DelimitedSchema? GetSchema( string[] values )
        {
            foreach (var matcher in matchers)
            {
                if (!matcher.Predicate( values ))
                {
                    continue;
                }
                matcher.Action?.Invoke();
                return matcher.Schema;
            }
            if (!defaultMatcher.Predicate( values ))
            {
                return null;
            }
            defaultMatcher.Action?.Invoke();
            return defaultMatcher.Schema;
        }

        private sealed class SchemaMatcher( DelimitedSchema? schema, Func<string[], bool> predicate )
        {
            public DelimitedSchema? Schema { get; } = schema;

            public Func<string[], bool> Predicate { get; } = predicate;

            public Action? Action { get; set; }
        }

        private sealed class DelimitedSchemaSelectorWhenBuilder( DelimitedSchemaSelector selector, Func<string[], bool> predicate ) : IDelimitedSchemaSelectorWhenBuilder
        {

            public IDelimitedSchemaSelectorUseBuilder Use( DelimitedSchema schema )
            {
                ArgumentNullException.ThrowIfNull( schema );
                var matcher = selector.Add( schema, predicate );
                return new DelimitedSchemaSelectorUseBuilder( matcher );
            }
        }

        private sealed class DelimitedSchemaSelectorUseBuilder( SchemaMatcher matcher ) : IDelimitedSchemaSelectorUseBuilder
        {

            public void OnMatch( Action action )
            {
                matcher.Action = action;
            }
        }
    }
}
