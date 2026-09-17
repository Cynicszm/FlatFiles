using System;
using FlatFiles.Properties;

namespace FlatFiles.TypeMapping
{
    internal sealed class DelimitedComplexPropertyMapping<TEntity>(
        IDelimitedTypeMapper<TEntity> mapper,
        IMemberAccessor member,
        int physicalIndex,
        int logicalIndex ) : IDelimitedComplexPropertyMapping, IMemberMapping
    {
        private string columnName = member.Name;
        private DelimitedOptions? options;
        private INullFormatter nullFormatter = FlatFiles.NullFormatter.Default;
        private IDefaultValue defaultValue = FlatFiles.DefaultValue.Disabled();
        private bool isNullable = true;
        private Func<string, string?>? preprocessor;
        private Func<IColumnContext?, string, string?>? onParsing;
        private Func<IColumnContext?, object?, object?>? onParsed;
        private Func<IColumnContext?, object?, object?>? onFormatting;
        private Func<IColumnContext?, string, string?>? onFormatted;

        public IColumnDefinition ColumnDefinition
        {
            get
            {
                var schema = mapper.GetSchema();
                var column = new DelimitedComplexColumn( columnName, schema )
                {
                    Options = options,
                    NullFormatter = nullFormatter,
                    DefaultValue = defaultValue,
                    IsNullable = isNullable,
#pragma warning disable CS0618 // Type or member is obsolete
                    Preprocessor = preprocessor,
#pragma warning restore CS0618 // Type or member is obsolete
                    OnParsing = onParsing,
                    OnParsed = onParsed,
                    OnFormatting = onFormatting,
                    OnFormatted = onFormatted
                };

                var mapperSource = (IMapperSource<TEntity>) mapper;
                var recordMapper = mapperSource.GetMapper();
                return new ComplexMapperColumn<TEntity>( schema, options ?? new DelimitedOptions(), column, recordMapper );
            }
        }

        public IMemberAccessor Member { get; } = member;

        public Action<IColumnContext?, object?, object?>? Reader => null;

        public Action<IColumnContext?, object?, object?[]>? Writer => null;

        public int PhysicalIndex { get; } = physicalIndex;

        public int LogicalIndex { get; } = logicalIndex;

        public IDelimitedComplexPropertyMapping ColumnName( string name )
        {
            if (string.IsNullOrWhiteSpace( name ))
            {
                throw new ArgumentException( Resources.BlankColumnName );
            }
            columnName = name;
            return this;
        }

        public IDelimitedComplexPropertyMapping WithOptions( DelimitedOptions? options )
        {
            this.options = options;
            return this;
        }

        public IDelimitedComplexPropertyMapping NullFormatter( INullFormatter formatter )
        {
            // A null here means the default, which the column's own setter applies.
            nullFormatter = formatter;
            return this;
        }

        public IDelimitedComplexPropertyMapping DefaultValue( IDefaultValue defaultValue )
        {
            this.defaultValue = defaultValue;
            return this;
        }

        public IDelimitedComplexPropertyMapping Nullable( bool isNullable )
        {
            this.isNullable = isNullable;
            return this;
        }

        public IDelimitedComplexPropertyMapping Preprocessor( Func<string, string?>? preprocessor )
        {
            this.preprocessor = preprocessor;
            return this;
        }

        public IDelimitedComplexPropertyMapping OnParsing( Func<IColumnContext?, string, string?>? handler )
        {
            onParsing = handler;
            return this;
        }

        public IDelimitedComplexPropertyMapping OnParsed( Func<IColumnContext?, object?, object?>? handler )
        {
            onParsed = handler;
            return this;
        }

        public IDelimitedComplexPropertyMapping OnFormatting( Func<IColumnContext?, object?, object?>? handler )
        {
            onFormatting = handler;
            return this;
        }

        public IDelimitedComplexPropertyMapping OnFormatted( Func<IColumnContext?, string, string?>? handler )
        {
            onFormatted = handler;
            return this;
        }
    }
}
