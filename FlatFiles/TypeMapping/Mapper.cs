using System;
using System.Linq;

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
