using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Represents a class that can dynamically map types based on the shap of the record.
    /// </summary>
    public sealed class DelimitedTypeMapperSelector
    {
        private readonly List<TypeMapperMatcher> matchers = [];
        private IDynamicDelimitedTypeMapper? defaultMapper;

        /// <summary>
        ///     Initialises a new instance of a DelimitedTypeMapperSelector.
        /// </summary>
        public DelimitedTypeMapperSelector()
        {
        }

        /// <summary>
        ///     Indicates that the given schema should be used when the predicate returns true.
        /// </summary>
        /// <param name="predicate">Indicates whether the schema should be used for a record.</param>
        /// <returns>An object for specifying which schema to use when the predicate matches.</returns>
        /// <remarks>Previously registered schemas will be used if their predicates match.</remarks>
        public IDelimitedTypeMapperSelectorWhenBuilder When( Func<string[], bool> predicate )
        {
            ArgumentNullException.ThrowIfNull( predicate );
            return new DelimitedTypeMapperSelectorWhenBuilder( this, predicate, null );
        }

        /// <summary>
        ///     Indicates that the given mapper should be used when the predicate returns true for the record's text.
        /// </summary>
        /// <param name="predicate">Indicates whether the mapper should be used for a record.</param>
        /// <returns>An object for specifying which mapper to use when the predicate matches.</returns>
        /// <remarks>
        ///     A predicate given to <see cref="When" /> needs the record split into its values and those values
        ///     copied out of the buffer they were read in, which is the one thing that stops a selected mapping
        ///     being read straight onto an entity. Where every predicate here reads the record's text instead, and
        ///     every mapping behind this selector can be read that way, records go onto their entities directly and
        ///     no value is ever boxed. Prefer it where a record can be recognised by a prefix, a length or a
        ///     character at a known position.
        /// </remarks>
        public IDelimitedTypeMapperSelectorWhenBuilder WhenText( RecordTextPredicate predicate )
        {
            ArgumentNullException.ThrowIfNull( predicate );
            return new DelimitedTypeMapperSelectorWhenBuilder( this, null, predicate );
        }

        /// <summary>
        ///     Provides the schema to use by default when no other matches are found.
        /// </summary>
        /// <param name="typeMapper">The default type mapper to use.</param>
        public void WithDefault<TEntity>( IDelimitedTypeMapper<TEntity>? typeMapper )
        {
            defaultMapper = (IDynamicDelimitedTypeMapper?) typeMapper;
        }

        /// <summary>
        ///     Provides the schema to use by default when no other matches are found.
        /// </summary>
        /// <param name="typeMapper">The default schema to use.</param>
        public void WithDefault( IDynamicDelimitedTypeMapper? typeMapper )
        {
            defaultMapper = typeMapper;
        }

        /// <summary>
        ///     Gets a typed reader for reading the objects from the file.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <param name="options">The separate value options to use.</param>
        /// <returns>The typed reader.</returns>
        public IDelimitedTypedReader<object> GetReader( TextReader reader, DelimitedOptions? options = null )
        {
            var selector = new DelimitedSchemaSelector();
            var valueReader = new DelimitedReader( reader, selector, options );
            var multiReader = new MultiplexingDelimitedTypedReader( valueReader );
            // Every mapping or none, and every predicate or none: the reader takes the assembled path for every
            // record once it is given an assembler, and a predicate that wants the record's values puts every
            // record back on the values path anyway.
            var assemblers = Assemblers( [.. matchers.Select( x => x.TypeMapper ), .. defaultMapper is null ? Array.Empty<IDynamicDelimitedTypeMapper>() : [defaultMapper]] );
            foreach (var matcher in matchers)
            {
                var mapper = matcher.TypeMapper;
                var typedReader = new Lazy<Func<IRecordContext, object?[], object?>>( GetReader( mapper ) );
                matcher.Register( selector, mapper.GetSchema() ).OnMatch( () =>
                {
                    multiReader.Deserialiser = typedReader.Value;
                    multiReader.Assembler = assemblers?[mapper];
                } );
            }
            if (defaultMapper is null)
            {
                Install( multiReader, assemblers );
                return multiReader;
            }
            var fallback = defaultMapper;
            var typeReader = new Lazy<Func<IRecordContext, object?[], object?>>( GetReader( fallback ) );
            selector.WithDefault( fallback.GetSchema() ).OnMatch( () =>
            {
                multiReader.Deserialiser = typeReader.Value;
                multiReader.Assembler = assemblers?[fallback];
            } );
            Install( multiReader, assemblers );
            return multiReader;
        }

        /// <summary>
        ///     Puts the reader on the assembled path, where every mapping behind it can be read that way and no
        ///     predicate asked for the record's values.
        /// </summary>
        private static void Install( MultiplexingDelimitedTypedReader multiReader, Dictionary<IDynamicDelimitedTypeMapper, IObjectAssembler>? assemblers )
        {
            if (assemblers is not null)
            {
                multiReader.Install();
            }
        }

        /// <summary>
        ///     An assembler per mapper, or null where any one of them cannot be read onto an entity without its
        ///     values being boxed on the way.
        /// </summary>
        private static Dictionary<IDynamicDelimitedTypeMapper, IObjectAssembler>? Assemblers( IDynamicDelimitedTypeMapper[] mappers )
        {
            var assemblers = new Dictionary<IDynamicDelimitedTypeMapper, IObjectAssembler>( mappers.Length );
            foreach (var mapper in mappers)
            {
                var assembler = ( (IMapperSource) mapper ).GetMapper().GetAssembler();
                if (assembler is null)
                {
                    return null;
                }
                assemblers[mapper] = assembler;
            }
            return assemblers;
        }

        private static Func<Func<IRecordContext, object?[], object?>> GetReader( IDynamicDelimitedTypeMapper typeMapper )
        {
            var source = (IMapperSource) typeMapper;
            var reader = source.GetMapper();
            return reader.GetReader;
        }

        internal void Add( IDynamicDelimitedTypeMapper typeMapper, Func<string[], bool>? values, RecordTextPredicate? text )
        {
            matchers.Add( new TypeMapperMatcher( typeMapper, values, text ) );
        }

        private sealed class TypeMapperMatcher( IDynamicDelimitedTypeMapper typeMapper, Func<string[], bool>? values, RecordTextPredicate? text )
        {
            public IDynamicDelimitedTypeMapper TypeMapper { get; } = typeMapper;

            /// <summary>
            ///     Registers this matcher's question with the schema selector the reader will use, over the values
            ///     or over the text, whichever it was given.
            /// </summary>
            /// <param name="selector">The selector the reader chooses a schema with.</param>
            /// <param name="schema">The schema this matcher's mapper describes.</param>
            /// <returns>The builder that attaches what happens on a match.</returns>
            public IDelimitedSchemaSelectorUseBuilder Register( DelimitedSchemaSelector selector, DelimitedSchema schema )
            {
                return text is not null
                    ? selector.WhenText( text ).Use( schema )
                    : selector.When( values! ).Use( schema );
            }
        }

        private sealed class DelimitedTypeMapperSelectorWhenBuilder( DelimitedTypeMapperSelector selector, Func<string[], bool>? values, RecordTextPredicate? text ) : IDelimitedTypeMapperSelectorWhenBuilder
        {

            public void Use<TEntity>( IDelimitedTypeMapper<TEntity> typeMapper )
            {
                var dynamicMapper = (IDynamicDelimitedTypeMapper) typeMapper;
                Use( dynamicMapper );
            }

            public void Use( IDynamicDelimitedTypeMapper typeMapper )
            {
                ArgumentNullException.ThrowIfNull( typeMapper );
                selector.Add( typeMapper, values, text );
            }
        }
    }
}
