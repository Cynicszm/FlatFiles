using System;

namespace FlatFiles.TypeMapping
{
    internal sealed class TimeOnlyPropertyMapping( TimeOnlyColumn column, IMemberAccessor member, int physicalIndex, int logicalIndex ) : ITimeOnlyPropertyMapping, IMemberMapping
    {
        public ITimeOnlyPropertyMapping ColumnName( string name )
        {
            column.ColumnName = name;
            return this;
        }

        public ITimeOnlyPropertyMapping InputFormat( string? format )
        {
            column.InputFormat = format;
            return this;
        }

        public ITimeOnlyPropertyMapping OutputFormat( string? format )
        {
            column.OutputFormat = format;
            return this;
        }

        public ITimeOnlyPropertyMapping FormatProvider( IFormatProvider? provider )
        {
            column.FormatProvider = provider;
            return this;
        }

        public ITimeOnlyPropertyMapping NullFormatter( INullFormatter formatter )
        {
            column.NullFormatter = formatter;
            return this;
        }

        public ITimeOnlyPropertyMapping DefaultValue( IDefaultValue defaultValue )
        {
            column.DefaultValue = defaultValue;
            return this;
        }

        public ITimeOnlyPropertyMapping Nullable( bool isNullable )
        {
            column.IsNullable = isNullable;
            return this;
        }

        public ITimeOnlyPropertyMapping OnParsing( Func<IColumnContext?, string, string?>? handler )
        {
            column.OnParsing = handler;
            return this;
        }

        public ITimeOnlyPropertyMapping OnParsed( Func<IColumnContext?, object?, object?>? handler )
        {
            column.OnParsed = handler;
            return this;
        }

        public ITimeOnlyPropertyMapping OnFormatting( Func<IColumnContext?, object?, object?>? handler )
        {
            column.OnFormatting = handler;
            return this;
        }

        public ITimeOnlyPropertyMapping OnFormatted( Func<IColumnContext?, string, string?>? handler )
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
