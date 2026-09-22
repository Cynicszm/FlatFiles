using System;

namespace FlatFiles.TypeMapping
{
    internal sealed class TimeSpanPropertyMapping(TimeSpanColumn column, IMemberAccessor member, int physicalIndex, int logicalIndex) : ITimeSpanPropertyMapping, IMemberMapping
    {
        public ITimeSpanPropertyMapping ColumnName(string name)
        {
            column.ColumnName = name;
            return this;
        }

        public ITimeSpanPropertyMapping InputFormat(string? format)
        {
            column.InputFormat = format;
            return this;
        }

        public ITimeSpanPropertyMapping OutputFormat(string? format)
        {
            column.OutputFormat = format;
            return this;
        }

        public ITimeSpanPropertyMapping FormatProvider(IFormatProvider? provider)
        {
            column.FormatProvider = provider;
            return this;
        }

        public ITimeSpanPropertyMapping NullFormatter(INullFormatter formatter)
        {
            column.NullFormatter = formatter;
            return this;
        }

        public ITimeSpanPropertyMapping DefaultValue(IDefaultValue defaultValue)
        {
            column.DefaultValue = defaultValue;
            return this;
        }

        public ITimeSpanPropertyMapping Nullable(bool isNullable)
        {
            column.IsNullable = isNullable;
            return this;
        }

        public ITimeSpanPropertyMapping OnParsing(Func<IColumnContext?, string, string?>? handler)
        {
            column.OnParsing = handler;
            return this;
        }

        public ITimeSpanPropertyMapping OnParsed(Func<IColumnContext?, object?, object?>? handler)
        {
            column.OnParsed = handler;
            return this;
        }

        public ITimeSpanPropertyMapping OnFormatting(Func<IColumnContext?, object?, object?>? handler)
        {
            column.OnFormatting = handler;
            return this;
        }

        public ITimeSpanPropertyMapping OnFormatted(Func<IColumnContext?, string, string?>? handler)
        {
            column.OnFormatted = handler;
            return this;
        }

        public IMemberAccessor Member { get; } = member;

        public Action<IColumnContext?, object?, object?>? Reader => null;

        public Action<IColumnContext?, object?, object?[]?>? Writer => null;

        public IColumnDefinition ColumnDefinition => column;

        public int PhysicalIndex { get; } = physicalIndex;

        public int LogicalIndex { get; } = logicalIndex;
    }
}
