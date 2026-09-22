using System;
using System.Buffers;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing durations.
    /// </summary>
    public sealed class TimeSpanColumn : ColumnDefinition<TimeSpan>
    {
        /// <summary>
        ///     Initializes a new instance of a TimeSpanColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public TimeSpanColumn( string columnName )
            : base( columnName )
        {
        }

        /// <summary>
        ///     Creates a column for reading and writing <see cref="TimeSpan"/> values where the days 
        ///     are stored as doubles in the flat file.
        /// </summary>
        /// <param name="column">The column for reading/writing doubles from the file.</param>
        /// <returns>A column for reading/writing <see cref="TimeSpan"/> values.</returns>
        public static IColumnDefinition FromDays( DoubleColumn column )
        {
            ArgumentNullException.ThrowIfNull( column );
            return new ConversionColumn<double, TimeSpan>( column, TimeSpan.FromDays, ts => ts.TotalDays );
        }

        /// <summary>
        ///     Creates a column for reading and writing <see cref="TimeSpan"/> values where the hours 
        ///     are stored as doubles in the flat file.
        /// </summary>
        /// <param name="column">The column for reading/writing doubles from the file.</param>
        /// <returns>A column for reading/writing <see cref="TimeSpan"/> values.</returns>
        public static IColumnDefinition FromHours( DoubleColumn column )
        {
            ArgumentNullException.ThrowIfNull( column );
            return new ConversionColumn<double, TimeSpan>( column, TimeSpan.FromHours, ts => ts.TotalHours );
        }

        /// <summary>
        ///     Creates a column for reading and writing <see cref="TimeSpan"/> values where the milliseconds 
        ///     are stored as doubles in the flat file.
        /// </summary>
        /// <param name="column">The column for reading/writing doubles from the file.</param>
        /// <returns>A column for reading/writing <see cref="TimeSpan"/> values.</returns>
        public static IColumnDefinition FromMilliseconds( DoubleColumn column )
        {
            ArgumentNullException.ThrowIfNull( column );
            return new ConversionColumn<double, TimeSpan>( column, TimeSpan.FromMilliseconds, ts => ts.TotalMilliseconds );
        }

        /// <summary>
        ///     Creates a column for reading and writing <see cref="TimeSpan"/> values where the minutes 
        ///     are stored as doubles in the flat file.
        /// </summary>
        /// <param name="column">The column for reading/writing doubles from the file.</param>
        /// <returns>A column for reading/writing <see cref="TimeSpan"/> values.</returns>
        public static IColumnDefinition FromMinutes( DoubleColumn column )
        {
            ArgumentNullException.ThrowIfNull( column );
            return new ConversionColumn<double, TimeSpan>( column, TimeSpan.FromMinutes, ts => ts.TotalMinutes );
        }

        /// <summary>
        ///     Creates a column for reading and writing <see cref="TimeSpan"/> values where the seconds 
        ///     are stored as doubles in the flat file.
        /// </summary>
        /// <param name="column">The column for reading/writing doubles from the file.</param>
        /// <returns>A column for reading/writing <see cref="TimeSpan"/> values.</returns>
        public static IColumnDefinition FromSeconds( DoubleColumn column )
        {
            ArgumentNullException.ThrowIfNull( column );
            return new ConversionColumn<double, TimeSpan>( column, TimeSpan.FromSeconds, ts => ts.TotalSeconds );
        }

        /// <summary>
        ///     Creates a column for reading and writing <see cref="TimeSpan"/> values where the ticks 
        ///     are stored as longs in the flat file.
        /// </summary>
        /// <param name="column">The column for reading/writing doubles from the file.</param>
        /// <returns>A column for reading/writing <see cref="TimeSpan"/> values.</returns>
        public static IColumnDefinition FromTicks( Int64Column column )
        {
            ArgumentNullException.ThrowIfNull( column );
            return new ConversionColumn<long, TimeSpan>( column, TimeSpan.FromTicks, ts => ts.Ticks );
        }

        /// <summary>
        ///     Gets or sets the format provider to use.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        ///     Gets or sets the format used to parse the <see cref="TimeSpan"/>.
        /// </summary>
        public string? InputFormat { get; set; }

        /// <summary>
        ///     Gets or sets the format used to write the <see cref="TimeSpan"/> to the flat file.
        /// </summary>
        public string? OutputFormat { get; set; }

        /// <inheritdoc />
        protected override TimeSpan OnParse( IColumnContext? context, string value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            return InputFormat is null ? TimeSpan.Parse( value, provider ) : TimeSpan.ParseExact( value, InputFormat, provider );
        }

        /// <summary>
        ///     Parses the given value without copying it out of the record it sits in.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        protected override TimeSpan OnParse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            return InputFormat is null ? TimeSpan.Parse( value, provider ) : TimeSpan.ParseExact( value, InputFormat, provider );
        }

        /// <inheritdoc />
        protected override string OnFormat( IColumnContext? context, TimeSpan value )
        {
            if (OutputFormat is null)
            {
                return value.ToString();
            }
            var provider = GetFormatProvider( context, FormatProvider );
            return value.ToString( OutputFormat, provider );
        }

        /// <summary>
        ///     Formats the given value straight into the destination buffer.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        protected override void OnFormat( IColumnContext? context, TimeSpan value, IBufferWriter<char> destination )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            WriteFormatted( destination, value, OutputFormat, provider );
        }
    }
}
