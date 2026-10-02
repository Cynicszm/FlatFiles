using System;
using System.Collections.Generic;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a class that can dynamically provide the schema based on the shape of a read record.
    /// </summary>
    public sealed class DelimitedSchemaSelector
    {
        private static readonly SchemaMatcher NonMatcher = new( null, null, _ => false );
        private readonly List<SchemaMatcher> matchers = [];
        private SchemaMatcher defaultMatcher = NonMatcher;

        /// <summary>
        ///     Initialises a new instance of a DelimitedSchemaSelector.
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
            return new DelimitedSchemaSelectorWhenBuilder( this, predicate, null );
        }

        /// <summary>
        ///     Indicates that the given schema should be used when the predicate returns true for the record's text.
        /// </summary>
        /// <param name="predicate">Indicates whether the schema should be used for a record.</param>
        /// <returns>An object for specifying which schema to use when the predicate matches.</returns>
        /// <exception cref="ArgumentNullException">The predicate is null.</exception>
        /// <remarks>
        ///     A record has to be split into its values before a predicate given to <see cref="When" /> can be asked
        ///     about it, and those values have to be copied out of the buffer they were read in, because the
        ///     predicate may keep them. A predicate given the record's text costs neither, so a selector whose
        ///     predicates all read the text lets the reader take each record straight onto an entity. Prefer it
        ///     where a record can be recognised by a prefix, a length or a character at a known position; use
        ///     <see cref="When" /> where the decision really needs the values.
        /// </remarks>
        public IDelimitedSchemaSelectorWhenBuilder WhenText( RecordTextPredicate predicate )
        {
            ArgumentNullException.ThrowIfNull( predicate );
            return new DelimitedSchemaSelectorWhenBuilder( this, null, predicate );
        }

        /// <summary>
        ///     Provides the schema to use by default when no other matches are found.
        /// </summary>
        /// <param name="schema">The default schema to use.</param>
        /// <returns>The current selector to allow for further customisation.</returns>
        public IDelimitedSchemaSelectorUseBuilder WithDefault( DelimitedSchema? schema )
        {
            defaultMatcher = schema is null ? NonMatcher : new SchemaMatcher( schema, null, _ => true );
            return new DelimitedSchemaSelectorUseBuilder( defaultMatcher );
        }

        private SchemaMatcher Add( DelimitedSchema schema, Func<string[], bool>? values, RecordTextPredicate? text )
        {
            var matcher = new SchemaMatcher( schema, values, text );
            matchers.Add( matcher );
            return matcher;
        }

        private SchemaMatcher AddSkip( Func<string[], bool>? values, RecordTextPredicate? text )
        {
            var matcher = new SchemaMatcher( null, values, text ) { IsSkipped = true };
            matchers.Add( matcher );
            return matcher;
        }

        /// <summary>
        ///     Whether any predicate registered here is given the record's values rather than its text, and so
        ///     whether the reader has to split every record before it can choose a schema for it.
        /// </summary>
        internal bool NeedsValues
        {
            get
            {
                foreach (var matcher in matchers)
                {
                    if (matcher.NeedsValues)
                    {
                        return true;
                    }
                }
                return defaultMatcher.NeedsValues;
            }
        }

        /// <summary>
        ///     The schema for this record, or null where none was chosen.
        /// </summary>
        /// <param name="record">The record's text.</param>
        /// <param name="values">The record's values, where any predicate here asks for them.</param>
        /// <param name="isSkipped">
        ///     True where a predicate matched and asked for the record to be passed over. Null comes back either
        ///     way, and the two mean different things: a record nothing matched is an error, and a record a
        ///     predicate asked to skip is not.
        /// </param>
        /// <returns>The schema, or null.</returns>
        internal DelimitedSchema? GetSchema( ReadOnlySpan<char> record, string[]? values, out bool isSkipped )
        {
            foreach (var matcher in matchers)
            {
                if (!matcher.Matches( record, values ))
                {
                    continue;
                }
                matcher.Action?.Invoke();
                isSkipped = matcher.IsSkipped;
                return matcher.Schema;
            }
            isSkipped = false;
            if (!defaultMatcher.Matches( record, values ))
            {
                return null;
            }
            defaultMatcher.Action?.Invoke();
            return defaultMatcher.Schema;
        }

        /// <summary>
        ///     One schema and the question that picks it: either over the record's values or over its text, never
        ///     both.
        /// </summary>
        private sealed class SchemaMatcher( DelimitedSchema? schema, Func<string[], bool>? values, RecordTextPredicate? text )
        {
            public DelimitedSchema? Schema { get; } = schema;

            /// <summary>Whether a match here means the record is passed over rather than read.</summary>
            public bool IsSkipped { get; init; }

            public Action? Action { get; set; }

            public bool NeedsValues => values is not null;

            public bool Matches( ReadOnlySpan<char> record, string[]? recordValues )
            {
                return text is not null ? text( record ) : values!( recordValues! );
            }
        }

        private sealed class DelimitedSchemaSelectorWhenBuilder( DelimitedSchemaSelector selector, Func<string[], bool>? values, RecordTextPredicate? text ) : IDelimitedSchemaSelectorWhenBuilder
        {

            public IDelimitedSchemaSelectorUseBuilder Use( DelimitedSchema schema )
            {
                ArgumentNullException.ThrowIfNull( schema );
                var matcher = selector.Add( schema, values, text );
                return new DelimitedSchemaSelectorUseBuilder( matcher );
            }

            public IDelimitedSchemaSelectorUseBuilder Skip()
            {
                return new DelimitedSchemaSelectorUseBuilder( selector.AddSkip( values, text ) );
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
