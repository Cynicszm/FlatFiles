using System;

namespace FlatFiles.TypeMapping
{
    internal sealed class DateOnlyPropertyMapping( DateOnlyColumn column, IMemberAccessor member, int physicalIndex, int logicalIndex ) : IDateOnlyPropertyMapping, IMemberMapping
    {
        public IDateOnlyPropertyMapping ColumnName( string name )
        {
            column.ColumnName = name;
            return this;
        }

        public IDateOnlyPropertyMapping InputFormat( string? format )
        {
            column.InputFormat = format;
            return this;
        }

        public IDateOnlyPropertyMapping OutputFormat( string? format )
        {
            column.OutputFormat = format;
            return this;
        }

        public IDateOnlyPropertyMapping FormatProvider( IFormatProvider? provider )
        {
            column.FormatProvider = provider;
            return this;
        }

        public IDateOnlyPropertyMapping NullFormatter( INullFormatter formatter )
        {
            column.NullFormatter = formatter;
            return this;
        }

        public IDateOnlyPropertyMapping DefaultValue( IDefaultValue defaultValue )
        {
            column.DefaultValue = defaultValue;
            return this;
        }

        public IDateOnlyPropertyMapping Nullable( bool isNullable )
        {
            column.IsNullable = isNullable;
            return this;
        }

        public IDateOnlyPropertyMapping Preprocessor( Func<string, string?>? preprocessor )
        {
#pragma warning disable CS0618 // Type or member is obsolete
            column.Preprocessor = preprocessor;
#pragma warning restore CS0618 // Type or member is obsolete
            return this;
        }

        public IDateOnlyPropertyMapping OnParsing( Func<IColumnContext?, string, string?>? handler )
        {
            column.OnParsing = handler;
            return this;
        }

        public IDateOnlyPropertyMapping OnParsed( Func<IColumnContext?, object?, object?>? handler )
        {
            column.OnParsed = handler;
            return this;
        }

        public IDateOnlyPropertyMapping OnFormatting( Func<IColumnContext?, object?, object?>? handler )
        {
            column.OnFormatting = handler;
            return this;
        }

        public IDateOnlyPropertyMapping OnFormatted( Func<IColumnContext?, string, string?>? handler )
        {
            column.OnFormatted = handler;
            return this;
        }

        public IMemberAccessor Member { get; } = member;

        public Action<IColumnContext?, object?, object?>? Reader => null;

        public Action<IColumnContext?, object?, object?[]>? Writer => null;

        public IColumnDefinition ColumnDefinition => column;

        public int PhysicalIndex { get; } = physicalIndex;

        public int LogicalIndex { get; } = logicalIndex;
    }
}
