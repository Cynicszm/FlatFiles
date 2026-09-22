using System;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Represents the mapping from a type property to a DateOnly column.
    /// </summary>
    public interface IDateOnlyPropertyMapping
    {
        /// <summary>
        ///     Sets the name of the column in the input or output file.
        /// </summary>
        /// <param name="name">The name of the column.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping ColumnName( string name );

        /// <summary>
        ///     Sets the date format the input is expected to be in.
        /// </summary>
        /// <param name="format">The format to expect.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping InputFormat( string? format );

        /// <summary>
        ///     Sets the date format to use for output.
        /// </summary>
        /// <param name="format">The format to use.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping OutputFormat( string? format );

        /// <summary>
        ///     Sets the format provider to use when reading and writing the date.
        /// </summary>
        /// <param name="provider">The provider to use.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping FormatProvider( IFormatProvider? provider );

        /// <summary>
        ///     Sets what value( s ) are treated as null.
        /// </summary>
        /// <param name="formatter">The formatter to use.</param>
        /// <returns>The property mapping for further configuration.</returns>
        /// <remarks>Passing null will cause the default formatter to be used.</remarks>
        IDateOnlyPropertyMapping NullFormatter( INullFormatter formatter );

        /// <summary>
        ///     Sets the default value to use when a null is encountered on a non-null property.
        /// </summary>
        /// <param name="defaultValue">The default value to use.</param>
        /// <returns>The property mapping for further configuration.</returns>
        /// <remarks>Passing null will cause an exception to be thrown for unexpected nulls.</remarks>
        IDateOnlyPropertyMapping DefaultValue( IDefaultValue defaultValue );

        /// <summary>
        ///     Sets whether the column is nullable.
        /// </summary>
        /// <param name="isNullable">Whether to set the column nullable or not.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping Nullable( bool isNullable );

        /// <summary>
        ///     Sets the function to run before the input is parsed.
        /// </summary>
        /// <param name="handler">A function to call before the textual value is parsed.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping OnParsing( Func<IColumnContext?, string, string?>? handler );

        /// <summary>
        ///     Sets the function to run after the input is parsed.
        /// </summary>
        /// <param name="handler">A function to call after the value is parsed.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping OnParsed( Func<IColumnContext?, object?, object?>? handler );

        /// <summary>
        ///     Sets the function to run before the output is formatted as a string.
        /// </summary>
        /// <param name="handler">A function to call before the value is formatted as a string.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping OnFormatting( Func<IColumnContext?, object?, object?>? handler );

        /// <summary>
        ///     Sets the function to run after the output is formatted as a string.
        /// </summary>
        /// <param name="handler">A function to call after the value is formatted as a string.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IDateOnlyPropertyMapping OnFormatted( Func<IColumnContext?, string, string?>? handler );
    }
}
