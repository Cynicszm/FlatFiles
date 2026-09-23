using System;
using System.Linq;
using System.Reflection;

namespace FlatFiles.TypeMapping
{
    internal sealed class Mapper<TEntity>( MemberLookup lookup, ICodeGenerator codeGenerator, IMemberAccessor? member ) : IMapper<TEntity>
    {
        private Func<IRecordContext, object?[], TEntity>? cachedReader;
        private Action<IRecordContext, TEntity, object?[]>? cachedWriter;

        public Mapper( MemberLookup lookup, ICodeGenerator codeGenerator )
            : this( lookup, codeGenerator, null )
        {
        }

        public IMemberAccessor? Member { get; } = member;

        public int LogicalCount => lookup.LogicalCount;

        public Func<IRecordContext, object?[], TEntity> GetReader()
        {
            if (cachedReader is not null)
            {
                return cachedReader;
            }
            var factory = lookup.GetFactory<TEntity>() ?? codeGenerator.GetFactory<TEntity>();
            var mappings = lookup.GetMappings();
            var memberMappings = GetReaderMemberMappings( mappings );
            var deserializer = codeGenerator.GetReader<TEntity>( memberMappings );
            var nestedMappers = GetNestedMappers( mappings );
            if (nestedMappers.Length != 0)
            {
                cachedReader = ( recordContext, values ) =>
                {
                    var entity = factory();
                    deserializer( recordContext, entity, values );
                    foreach (var nestedMapper in nestedMappers)
                    {
                        var nestedReader = nestedMapper.GetReader();
                        var result = nestedReader( recordContext, values );
                        nestedMapper.Member!.SetValue( entity!, result );
                    }
                    return entity;
                };
            }
            else
            {
                cachedReader = ( recordContext, values ) =>
                {
                    var entity = factory();
                    deserializer( recordContext, entity, values );
                    return entity;
                };
            }
            return cachedReader;
        }

        public TEntity CreateEntity()
        {
            var factory = lookup.GetFactory<TEntity>() ?? codeGenerator.GetFactory<TEntity>();
            return factory();
        }

        /// <summary>
        ///     Builds one setter per column, or answers null where anything about the mapping means a value has to
        ///     travel as an object: a custom reader, a nested entity, a field rather than a property, a property
        ///     without a setter, a column whose type does not match the member's, or a column carrying a hook.
        /// </summary>
        public IColumnSetter<TEntity>[]? GetColumnSetters()
        {
            if (cachedSetters is not null)
            {
                return cachedSetters.Length == 0 ? null : cachedSetters;
            }
            cachedSetters = BuildColumnSetters() ?? [];
            return cachedSetters.Length == 0 ? null : cachedSetters;
        }

        private IColumnSetter<TEntity>[]? BuildColumnSetters()
        {
            // Building one of these closes a generic type over the column's type, which a runtime without dynamic
            // code cannot do for a value type it was not built with.
            if (!DynamicCode.IsSupported || typeof( TEntity ).IsValueType || Member is not null)
            {
                return null;
            }
            var mappings = lookup.GetMappings();
            if (mappings.Any( m => m.Member?.ParentAccessor is not null ))
            {
                return null;
            }
            var readerMappings = GetReaderMemberMappings( mappings );
            if (readerMappings.Length == 0 || readerMappings.Length != lookup.LogicalCount)
            {
                return null;
            }
            var setters = new IColumnSetter<TEntity>[readerMappings.Length];
            foreach (var mapping in readerMappings)
            {
                var setter = BuildColumnSetter( mapping );
                if (setter is null || mapping.LogicalIndex < 0 || mapping.LogicalIndex >= setters.Length)
                {
                    return null;
                }
                setters[mapping.LogicalIndex] = setter;
            }
            return Array.Exists( setters, s => s is null ) ? null : setters;
        }

        private static IColumnSetter<TEntity>? BuildColumnSetter( IMemberMapping mapping )
        {
            if (mapping.Reader is not null || mapping.Member is null)
            {
                return null;
            }
            if (mapping.Member.MemberInfo is not PropertyInfo property)
            {
                return null;
            }
            if (mapping.ColumnDefinition is IMetadataColumn)
            {
                // Its value comes from the context rather than the record, and it is never a ColumnDefinition<T>.
                return null;
            }
            // Public only: a non-public setter cannot be reached from generated code either, and a mapping that
            // uses one should keep failing the way it does today rather than working only on this path.
            var assign = property.GetSetMethod( false );
            if (assign is null || mapping.ColumnDefinition is not ColumnDefinition definition)
            {
                return null;
            }
            var columnType = GetTypedColumnType( definition );
            if (columnType is null || !SupportsTypedParse( definition ))
            {
                return null;
            }
            var valueType = columnType.GetGenericArguments()[0];
            var memberType = property.PropertyType;
            if (memberType == valueType)
            {
                var assigner = assign.CreateDelegate( typeof( Action<,> ).MakeGenericType( typeof( TEntity ), valueType ) );
                var setterType = typeof( ColumnSetter<,> ).MakeGenericType( typeof( TEntity ), valueType );
                return (IColumnSetter<TEntity>) Activator.CreateInstance( setterType, definition, assigner, mapping.Member )!;
            }
            if (valueType.IsValueType && memberType == typeof( Nullable<> ).MakeGenericType( valueType ))
            {
                var assigner = assign.CreateDelegate( typeof( Action<,> ).MakeGenericType( typeof( TEntity ), memberType ) );
                var setterType = typeof( NullableColumnSetter<,> ).MakeGenericType( typeof( TEntity ), valueType );
                return (IColumnSetter<TEntity>) Activator.CreateInstance( setterType, definition, assigner )!;
            }
            return null;
        }

        private static Type? GetTypedColumnType( ColumnDefinition definition )
        {
            for (var type = definition.GetType(); type is not null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof( ColumnDefinition<> ))
                {
                    return type;
                }
            }
            return null;
        }

        private static bool SupportsTypedParse( ColumnDefinition definition )
        {
            var property = GetTypedColumnType( definition )!.GetProperty( "SupportsTypedParse", BindingFlags.Instance | BindingFlags.NonPublic );
            return property?.GetValue( definition ) is true;
        }

        private IColumnSetter<TEntity>[]? cachedSetters;

        Func<IRecordContext, object?[], object?> IMapper.GetReader()
        {
            var reader = GetReader();
            return ( metadata, values ) => reader( metadata, values );
        }

        public Action<IRecordContext, TEntity, object?[]> GetWriter()
        {
            if (cachedWriter is not null)
            {
                return cachedWriter;
            }
            var mappings = lookup.GetMappings();
            var memberMappings = GetWriterMemberMappings( mappings );
            var serializer = codeGenerator.GetWriter<TEntity>( memberMappings );
            var nestedMappers = GetNestedMappers( mappings );
            if (nestedMappers.Length != 0)
            {
                cachedWriter = ( metadata, entity, values ) =>
                {
                    serializer( metadata, entity, values );
                    foreach (var nestedMapper in nestedMappers)
                    {
                        var nested = nestedMapper.Member!.GetValue( entity! );
                        var writer = nestedMapper.GetWriter();
                        writer( metadata, nested, values );
                    }
                };
            }
            else
            {
                cachedWriter = serializer;
            }
            return cachedWriter;
        }

        Action<IRecordContext, object?, object?[]> IMapper.GetWriter()
        {
            var writer = GetWriter();
            return ( metadata, entity, values ) => writer( metadata, (TEntity) entity!, values );
        }

        private IMemberMapping[] GetReaderMemberMappings( IMemberMapping[] mappings )
        {
            var memberMappings = mappings
                .Where( m => m.Member is not null || m.Reader is not null )
                .Where( m => Member?.Name == m.Member?.ParentAccessor?.Name )
                .ToArray();
            return memberMappings;
        }

        private IMemberMapping[] GetWriterMemberMappings( IMemberMapping[] mappings )
        {
            var memberMappings = mappings
                .Where( m => m.Member is not null || m.Writer is not null )
                .Where( m => Member?.Name == m.Member?.ParentAccessor?.Name )
                .ToArray();
            return memberMappings;
        }

        private IMapper[] GetNestedMappers( IMemberMapping[] mappings )
        {
            var mappers = mappings
                .Where( m => m.Member is not null )
                .Where( m => Member?.Name != m.Member!.ParentAccessor?.Name )
                .Where( m => m.Member!.Name.StartsWith( Member?.Name ?? string.Empty ) )
                .Select( GetParentAccessor )
                .GroupBy( p => p.Name )
                .Select( g => g.First() )
                .Select( GetMapper )
                .ToArray();
            return mappers;
        }

        private IMemberAccessor GetParentAccessor( IMemberMapping mapping )
        {
            var accessorName = Member?.Name ?? string.Empty;
            var childAccessor = mapping.Member!;
            var parentAccessor = childAccessor.ParentAccessor;
            while (parentAccessor is not null && accessorName != parentAccessor.Name)
            {
                childAccessor = parentAccessor;
                parentAccessor = childAccessor.ParentAccessor;
            }
            return childAccessor;
        }

        private IMapper GetMapper( IMemberAccessor childMember )
        {
            var entityType = childMember.Type;
            var mapperType = typeof( Mapper<> ).MakeGenericType( entityType );
            var mapper = (IMapper) Activator.CreateInstance( mapperType, lookup, codeGenerator, childMember )!;
            return mapper;
        }
    }
}
