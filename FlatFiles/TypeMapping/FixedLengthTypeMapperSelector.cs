using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Represents a class that can dynamically map types based on the shap of the record.
    /// </summary>
    public sealed class FixedLengthTypeMapperSelector
    {
        private readonly List<TypeMapperMatcher> matchers = [];
        private IDynamicFixedLengthTypeMapper? defaultMapper;

        /// <summary>
        ///     Initialises a new instance of a FixedLengthTypeMapperSelector.
        /// </summary>
        public FixedLengthTypeMapperSelector()
        {
        }

        /// <summary>
        ///     Indicates that the given schema should be used when the predicate returns true.
        /// </summary>
        /// <param name="predicate">Indicates whether the schema should be used for a record.</param>
        /// <returns>An object for specifying which schema to use when the predicate matches.</returns>
        /// <exception cref="ArgumentNullException">The predicate is null.</exception>
        /// <remarks>Previously registered schemas will be used if their predicates match.</remarks>
        public IFixedLengthTypeMapperSelectorWhenBuilder When( Func<string, bool> predicate )
        {
            ArgumentNullException.ThrowIfNull( predicate );
            return new FixedLengthTypeMapperSelectorWhenBuilder( this, predicate );
        }

        /// <summary>
        ///     Provides the schema to use by default when no other matches are found.
        /// </summary>
        /// <param name="typeMapper">The default type mapper to use.</param>
        /// <returns>The current selector to allow for further customisation.</returns>
        public void WithDefault<TEntity>( IFixedLengthTypeMapper<TEntity>? typeMapper )
        {
            defaultMapper = (IDynamicFixedLengthTypeMapper?) typeMapper;
        }

        /// <summary>
        ///     Provides the schema to use by default when no other matches are found.
        /// </summary>
        /// <param name="typeMapper">The default schema to use.</param>
        /// <returns>The current selector to allow for further customisation.</returns>
        public void WithDefault( IDynamicFixedLengthTypeMapper? typeMapper )
        {
            defaultMapper = typeMapper;
        }

        /// <summary>
        ///     Gets a typed reader for reading the objects from the file.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <param name="options">The separate value options to use.</param>
        /// <returns>The typed reader.</returns>
        public IFixedLengthTypedReader<object?> GetReader( TextReader reader, FixedLengthOptions? options = null )
        {
            var selector = new FixedLengthSchemaSelector();
            var valueReader = new FixedLengthReader( reader, selector, options );
            var multiReader = new MultiplexingFixedLengthTypedReader( valueReader );
            // Every mapping or none: the reader takes the assembled path for every record once it is given an
            // assembler, so one mapping that cannot be read that way puts them all back on the values path.
            var assemblers = Assemblers( [.. matchers.Select( x => x.TypeMapper ), .. defaultMapper is null ? Array.Empty<IDynamicFixedLengthTypeMapper>() : [defaultMapper]] );
            foreach (var matcher in matchers)
            {
                var mapper = matcher.TypeMapper;
                var typedReader = new Lazy<Func<IRecordContext, object?[], object?>>( GetReader( mapper ) );
                selector.When( matcher.Predicate ).Use( mapper.GetSchema() ).OnMatch( () =>
                {
                    multiReader.Deserializer = typedReader.Value;
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
                multiReader.Deserializer = typeReader.Value;
                multiReader.Assembler = assemblers?[fallback];
            } );
            Install( multiReader, assemblers );
            return multiReader;
        }

        private static void Install( MultiplexingFixedLengthTypedReader multiReader, Dictionary<IDynamicFixedLengthTypeMapper, IObjectAssembler>? assemblers )
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
        private static Dictionary<IDynamicFixedLengthTypeMapper, IObjectAssembler>? Assemblers( IDynamicFixedLengthTypeMapper[] mappers )
        {
            var assemblers = new Dictionary<IDynamicFixedLengthTypeMapper, IObjectAssembler>( mappers.Length );
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

        private static Func<Func<IRecordContext, object?[], object?>> GetReader( IDynamicFixedLengthTypeMapper typeMapper )
        {
            var source = (IMapperSource) typeMapper;
            var reader = source.GetMapper();
            return reader.GetReader;
        }

        internal void Add( IDynamicFixedLengthTypeMapper typeMapper, Func<string, bool> predicate )
        {
            matchers.Add( new TypeMapperMatcher( typeMapper, predicate ) );
        }

        private sealed class TypeMapperMatcher( IDynamicFixedLengthTypeMapper typeMapper, Func<string, bool> predicate )
        {
            public IDynamicFixedLengthTypeMapper TypeMapper { get; } = typeMapper;

            public Func<string, bool> Predicate { get; } = predicate;
        }

        private sealed class FixedLengthTypeMapperSelectorWhenBuilder( FixedLengthTypeMapperSelector selector, Func<string, bool> predicate ) : IFixedLengthTypeMapperSelectorWhenBuilder
        {

            public void Use<TEntity>( IFixedLengthTypeMapper<TEntity> typeMapper )
            {
                var dynamicMapper = (IDynamicFixedLengthTypeMapper) typeMapper;
                Use( dynamicMapper );
            }

            public void Use( IDynamicFixedLengthTypeMapper typeMapper )
            {
                ArgumentNullException.ThrowIfNull( typeMapper );
                selector.Add( typeMapper, predicate );
            }
        }
    }
}
