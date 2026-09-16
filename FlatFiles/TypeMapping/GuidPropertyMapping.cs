using System;

namespace FlatFiles.TypeMapping
{
    internal sealed class GuidPropertyMapping(GuidColumn column, IMemberAccessor member, int physicalIndex, int logicalIndex) : IGuidPropertyMapping, IMemberMapping
    {
        public IGuidPropertyMapping ColumnName(string name)
        {
            column.ColumnName = name;
            return this;
        }

        public IGuidPropertyMapping InputFormat(string? format)
        {
            column.InputFormat = format;
            return this;
        }

        public IGuidPropertyMapping OutputFormat(string? format)
        {
            column.OutputFormat = format;
            return this;
        }

        public IGuidPropertyMapping NullFormatter(INullFormatter formatter)
        {
            column.NullFormatter = formatter;
            return this;
        }

        public IGuidPropertyMapping DefaultValue(IDefaultValue defaultValue)
        {
            column.DefaultValue = defaultValue;
            return this;
        }

        public IGuidPropertyMapping Nullable(bool isNullable)
        {
            column.IsNullable = isNullable;
            return this;
        }

        public IGuidPropertyMapping Preprocessor(Func<string, string?>? preprocessor)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            column.Preprocessor = preprocessor;
#pragma warning restore CS0618 // Type or member is obsolete
            return this;
        }

        public IGuidPropertyMapping OnParsing(Func<IColumnContext?, string, string?>? handler)
        {
            column.OnParsing = handler;
            return this;
        }

        public IGuidPropertyMapping OnParsed(Func<IColumnContext?, object?, object?>? handler)
        {
            column.OnParsed = handler;
            return this;
        }

        public IGuidPropertyMapping OnFormatting(Func<IColumnContext?, object?, object?>? handler)
        {
            column.OnFormatting = handler;
            return this;
        }

        public IGuidPropertyMapping OnFormatted(Func<IColumnContext?, string, string?>? handler)
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
