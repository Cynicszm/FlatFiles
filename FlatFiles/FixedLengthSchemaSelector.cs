using System;
using System.Collections.Generic;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a class that can dynamically provide the schema based on the shape of a read record.
    /// </summary>
    public sealed class FixedLengthSchemaSelector
    {
        private readonly List<SchemaMatcher> matchers = [];
        private SchemaMatcher? defaultMatcher;

        /// <summary>
        ///     Indicates that the given schema should be used when the predicate returns true.
        /// </summary>
        /// <param name="predicate">Indicates whether the schema should be used for a record.</param>
        /// <returns>An object for specifying which schema to use when the predicate matches.</returns>
        /// <exception cref="ArgumentNullException">The predicate is null.</exception>
        /// <remarks>Previously registered schemas will be used if their predicates match.</remarks>
        public IFixedLengthSchemaSelectorWhenBuilder When( Func<string, bool> predicate )
        {
            ArgumentNullException.ThrowIfNull( predicate );
            return new FixedLengthSchemaSelectorWhenBuilder( this, predicate );
        }

        /// <summary>
        ///     Provides the schema to use by default when no other matches are found.
        /// </summary>
        /// <param name="schema">The default schema to use.</param>
        /// <returns>The current selector to allow for further customisation.</returns>
        public IFixedLengthSchemaSelectorUseBuilder WithDefault( FixedLengthSchema? schema )
        {
            defaultMatcher = schema is null ? null : new SchemaMatcher( schema, _ => true );
            return new FixedLengthSchemaSelectorUseBuilder( defaultMatcher );
        }

        private SchemaMatcher Add( FixedLengthSchema schema, Func<string, bool> predicate )
        {
            var matcher = new SchemaMatcher( schema, predicate );
            matchers.Add( matcher );
            return matcher;
        }

        private SchemaMatcher AddSkip( Func<string, bool> predicate )
        {
            var matcher = new SchemaMatcher( null, predicate ) { IsSkipped = true };
            matchers.Add( matcher );
            return matcher;
        }

        /// <summary>
        ///     The schema for this record, or null where none was chosen.
        /// </summary>
        /// <param name="record">The record's text.</param>
        /// <param name="isSkipped">
        ///     True where a predicate matched and asked for the record to be passed over. Null comes back either
        ///     way, and the two mean different things: a record nothing matched is an error, and a record a
        ///     predicate asked to skip is not.
        /// </param>
        /// <returns>The schema, or null.</returns>
        internal FixedLengthSchema? GetSchema( string record, out bool isSkipped )
        {
            foreach (var matcher in matchers)
            {
                if (!matcher.Predicate( record ))
                {
                    continue;
                }
                matcher.Action?.Invoke();
                isSkipped = matcher.IsSkipped;
                return matcher.Schema;
            }
            isSkipped = false;
            if (defaultMatcher is null || !defaultMatcher.Predicate( record ))
            {
                return null;
            }
            defaultMatcher.Action?.Invoke();
            return defaultMatcher.Schema;
        }

        private sealed class SchemaMatcher( FixedLengthSchema? schema, Func<string, bool> predicate )
        {
            public FixedLengthSchema? Schema { get; } = schema;

            /// <summary>Whether a match here means the record is passed over rather than read.</summary>
            public bool IsSkipped { get; init; }

            public Func<string, bool> Predicate { get; } = predicate;

            public Action? Action { get; set; }
        }

        private sealed class FixedLengthSchemaSelectorWhenBuilder( FixedLengthSchemaSelector selector, Func<string, bool> predicate ) : IFixedLengthSchemaSelectorWhenBuilder
        {

            public IFixedLengthSchemaSelectorUseBuilder Use( FixedLengthSchema schema )
            {
                ArgumentNullException.ThrowIfNull( schema );
                var matcher = selector.Add( schema, predicate );
                return new FixedLengthSchemaSelectorUseBuilder( matcher );
            }

            public IFixedLengthSchemaSelectorUseBuilder Skip()
            {
                return new FixedLengthSchemaSelectorUseBuilder( selector.AddSkip( predicate ) );
            }

        }

        private sealed class FixedLengthSchemaSelectorUseBuilder( SchemaMatcher? matcher ) : IFixedLengthSchemaSelectorUseBuilder
        {

            public void OnMatch( Action? action )
            {
                matcher?.Action = action;
            }
        }
    }
}
