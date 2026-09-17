using System;

namespace FlatFiles.TypeMapping
{
    internal sealed class ReflectionCodeGenerator : ICodeGenerator
    {
        public Func<TEntity> GetFactory<TEntity>()
        {
            return () => (TEntity) Activator.CreateInstance( typeof( TEntity ), true )!;
        }

        public Action<IRecordContext, TEntity, object?[]> GetReader<TEntity>( IMemberMapping[] mappings )
        {
            void Reader( IRecordContext recordContext, TEntity entity, object?[] values )
            {
                for (var index = 0; index != mappings.Length; ++index)
                {
                    var mapping = mappings[index];
                    if (mapping.Member is not null)
                    {
                        var value = values[mapping.LogicalIndex];
                        mapping.Member.SetValue( entity!, value );
                    }
                    else if (mapping.Reader is not null)
                    {
                        var columnContext = GetColumnContext( recordContext, mapping );
                        var value = values[mapping.LogicalIndex];
                        mapping.Reader( columnContext, entity!, value );
                    }
                }
            }

            return Reader;
        }

        public Action<IRecordContext, TEntity, object?[]> GetWriter<TEntity>( IMemberMapping[] mappings )
        {
            void Writer( IRecordContext recordContext, TEntity entity, object?[] values )
            {
                for (var index = 0; index != mappings.Length; ++index)
                {
                    var mapping = mappings[index];
                    if (mapping.Member is not null)
                    {
                        var value = mapping.Member.GetValue( entity! );
                        values[mapping.LogicalIndex] = value;
                    }
                    else if (mapping.Writer is not null)
                    {
                        var columnContext = GetColumnContext( recordContext, mapping );
                        mapping.Writer( columnContext, entity, values );
                    }
                }
            }

            return Writer;
        }

        private static IColumnContext GetColumnContext( IRecordContext recordContext, IMemberMapping mapping )
        {
            var columnContext = new ColumnContext( recordContext, mapping.PhysicalIndex, mapping.LogicalIndex );
            return columnContext;
        }
    }
}
