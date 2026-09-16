
using System;
using System.Data;
using System.Linq;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    /// Provides extensions methods for populating a DataTable using flat files.
    /// </summary>
    public static class DataTableExtensions
    {
        extension(DataTable table)
        {
            /// <summary>
            /// Loads the contents returned by the given reader into the DataTable.
            /// </summary>
            /// <param name="reader">The reader to use to extract the file schema and data.</param>
            /// <param name="loadOption">Controls how values from the flat file will be applied to existing rows.</param>
            /// <param name="errorHandler">A <see cref="FillErrorEventHandler"/> delegate to call when an error occurs while loading data.</param>
            /// <exception cref="ArgumentNullException">The table is null.</exception>
            /// <exception cref="ArgumentNullException">The reader is null.</exception>
            public void ReadFlatFile(IReader reader, LoadOption loadOption = LoadOption.PreserveChanges, FillErrorEventHandler? errorHandler = null)
            {
                ArgumentNullException.ThrowIfNull( table );
                ArgumentNullException.ThrowIfNull( reader );
                var fileReader = new FlatFileDataReader(reader);
                table.Load(fileReader, loadOption, errorHandler);
            }

            /// <summary>
            /// Writes the data table contents to the writer.
            /// </summary>
            /// <param name="writer">The writer to write the values to.</param>
            /// <exception cref="ArgumentNullException">The table is null.</exception>
            /// <exception cref="ArgumentNullException">The writer is null.</exception>
            public void WriteFlatFile(IWriter writer)
            {
                ArgumentNullException.ThrowIfNull( table );
                ArgumentNullException.ThrowIfNull( writer );
                var schema = writer.GetSchema();
                if (schema == null)
                {
                    throw new FlatFileException(Resources.SchemaNotDefined);
                }
                var columnIndexes = schema.ColumnDefinitions
                    .Where(c => !c.IsIgnored)
                    .Select(c => table.Columns.IndexOf(c.ColumnName))
                    .ToArray();
                var values = new object?[columnIndexes.Length];
                foreach (DataRow? row in table.Rows)
                {
                    for (int index = 0; index != values.Length; ++index)
                    {
                        int columnIndex = columnIndexes[index];
                        if (columnIndex != -1)
                        {
                            values[index] = row!.IsNull(columnIndex) ? null : row[columnIndex];
                        }
                    }
                    writer.Write(values);
                }
            }
        }
    }
}

