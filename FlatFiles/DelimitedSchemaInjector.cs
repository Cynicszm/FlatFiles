using System;
using System.Collections.Generic;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a class that can dynamically provide the schema based on the shape of the data being written.
    /// </summary>
    public sealed class DelimitedSchemaInjector
    {
        private readonly List<SchemaMatcher> matchers = [];
        private SchemaMatcher? defaultMatcher;

        /// <summary>
        ///     Initializes a new instance of a DelimitedSchemaInjector.
        /// </summary>
        public DelimitedSchemaInjector()
        {
        }

        /// <summary>
        ///     Indicates that the given schema should be used when the predicate returns true.
        /// </summary>
        /// <param name="predicate">Indicates whether the schema should be used for a record.</param>
        /// <returns>An object for specifying which schema to use when the predicate matches.</returns>
        /// <exception cref="ArgumentNullException">The predicate is null.</exception>
        /// <remarks>Previously registered schemas will be used if their predicates match.</remarks>
        public IDelimitedSchemaInjectorWhenBuilder When( Func<object?[], bool> predicate )
        {
            ArgumentNullException.ThrowIfNull( predicate );
            return new DelimitedSchemaInjectorWhenBuilder( this, predicate );
        }

        /// <summary>
        ///     Provides the schema to use by default when no other matches are found.
        /// </summary>
        /// <param name="schema">The default schema to use.</param>
        /// <returns>The current selector to allow for further customization.</returns>
        public void WithDefault( DelimitedSchema? schema )
        {
            defaultMatcher = schema is null ? null : new SchemaMatcher( schema, _ => true );
        }

        private void Add( DelimitedSchema schema, Func<object?[], bool> predicate )
        {
            var matcher = new SchemaMatcher( schema, predicate );
            matchers.Add( matcher );
        }

        internal DelimitedSchema GetSchema( object?[] values )
        {
            foreach (var matcher in matchers)
            {
                if (matcher.Predicate( values ))
                {
                    return matcher.Schema;
                }
            }
            if (defaultMatcher is not null && defaultMatcher.Predicate( values ))
            {
                return defaultMatcher.Schema;
            }
            throw new FlatFileException( Resources.MissingMatcher );
        }

        private sealed class SchemaMatcher( DelimitedSchema schema, Func<object?[], bool> predicate )
        {
            public DelimitedSchema Schema { get; } = schema;

            public Func<object?[], bool> Predicate { get; } = predicate;
        }

        private sealed class DelimitedSchemaInjectorWhenBuilder( DelimitedSchemaInjector injector, Func<object?[], bool> predicate ) : IDelimitedSchemaInjectorWhenBuilder
        {

            public void Use( DelimitedSchema schema )
            {
                ArgumentNullException.ThrowIfNull( schema );
                injector.Add( schema, predicate );
            }
        }
    }
}
