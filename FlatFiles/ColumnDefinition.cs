using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Defines a column that is part of a record schema.
    /// </summary>
    public abstract class ColumnDefinition : IColumnDefinition
    {
        /// <summary>
        ///     Initialises a new instance of a ColumnDefinition.
        /// </summary>
        /// <param name="columnName">The name of the column to define.</param>
        protected ColumnDefinition( string columnName )
            : this( columnName, false )
        {
        }

        /// <summary>
        ///     Initialises a new instance of a ColumnDefinition.
        /// </summary>
        /// <param name="columnName">The name of the column to define.</param>
        /// <param name="isIgnored">Specifies whether the value in the column appears in the parsed record.</param>
        internal ColumnDefinition( string? columnName, bool isIgnored )
        {
            IsIgnored = isIgnored; // Set ignored first or ColumnName setter fails!
            ColumnName = columnName;
        }

        /// <summary>
        ///     Gets the name of the column.
        /// </summary>
        public string? ColumnName
        {
            get;
            internal set 
            {
                value = value?.Trim();
                if (!IsIgnored && string.IsNullOrEmpty( value ))
                {
                    throw new ArgumentException( Resources.BlankColumnName );
                }
                field = value;
            }
        }

        /// <summary>
        ///     Gets whether the value in this column is returned as a result.
        /// </summary>
        public bool IsIgnored { get; }

        /// <summary>
        ///     Gets or sets whether nulls are allowed for the column.
        /// </summary>
        public bool IsNullable { get; set; } = true;

        /// <inheritdoc/>
        public virtual bool IsComplex => false;

        /// <inheritdoc/>
        /// <remarks>
        ///     One of the library's own columns needs its context only when something that can look at it is attached:
        ///     a parsing or formatting hook, a null formatter or default value from outside the library, or a default
        ///     value built from a delegate. A subclass declared outside the library may read the context in its own
        ///     parsing or formatting, so it is always given one.
        /// </remarks>
        public virtual bool IsColumnContextRequired =>
            OnParsing is not null || OnParsed is not null || OnFormatting is not null || OnFormatted is not null
            || NullFormatter is not FlatFiles.NullFormatter
            || DefaultValue is not DefaultValue { UsesColumnContext: false }
            || IsComplex
            || this is IMetadataColumn
            || !IsLibraryColumn;

        /// <summary>
        ///     Whether this column's type is declared in the library. Asked once per column rather than once per value,
        ///     because the type's assembly is a reflection lookup and the answer never changes.
        /// </summary>
        private bool IsLibraryColumn => isLibraryColumn ??= GetType().Assembly == typeof( ColumnDefinition ).Assembly;

        private bool? isLibraryColumn;

        /// <summary>
        ///     Gets or sets the default value to use when a null is encountered on a non-nullable column.
        /// </summary>
        [AllowNull]
        public IDefaultValue DefaultValue
        {
            get;
            set => field = value ?? FlatFiles.DefaultValue.Disabled();
        } = FlatFiles.DefaultValue.Disabled();

        /// <summary>
        ///     Gets or sets the null formatter instance used to read/write null values.
        /// </summary>
        [AllowNull]
        public INullFormatter NullFormatter
        {
            get;
            set => field = value ?? FlatFiles.NullFormatter.Default;
        } = FlatFiles.NullFormatter.Default;

        /// <summary>
        ///     Gets or sets a function used to pre-process input before trying to parse it.
        /// </summary>
        public Func<IColumnContext?, string, string?>? OnParsing { get; set; }

        /// <summary>
        ///     Gets or sets a function used to post-process input after parsing it.
        /// </summary>
        public Func<IColumnContext?, object?, object?>? OnParsed { get; set; }

        /// <summary>
        ///     Gets or sets a function used to pre-process output before trying to format it.
        /// </summary>
        public Func<IColumnContext?, object?, object?>? OnFormatting { get; set; }

        /// <summary>
        ///     Gets or sets a function used to post-process output after formatting it.
        /// </summary>
        public Func<IColumnContext?, string, string?>? OnFormatted { get; set; }

        /// <summary>
        ///     Gets the type of the values in the column.
        /// </summary>
        public abstract Type ColumnType { get; }

        /// <summary>
        ///     Parses the given value and returns the parsed object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        public abstract object? Parse( IColumnContext? context, string value );

        /// <summary>
        ///     Parses the given value without the caller first copying it out of the record it sits in. The default
        ///     copies the text and parses the string; <see cref="ColumnDefinition{T}" /> parses the characters
        ///     themselves wherever the column's type can be read from them.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        public virtual object? Parse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            return Parse( context, value.ToString() );
        }

        /// <summary>
        ///     Removes any leading or trailing whitespace from the value.
        /// </summary>
        /// <param name="value">The value to trim.</param>
        /// <returns>The trimmed value.</returns>
        protected internal static string TrimValue( string value )
        {
            return value.Trim();
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        public abstract string Format( IColumnContext? context, object? value );

        /// <summary>
        ///     Formats the given object and appends the result to a buffer instead of returning a string. The default
        ///     writes the string that <see cref="Format(IColumnContext?, object?)" /> returns; the built-in column
        ///     types override it to format directly into the buffer.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        public virtual void Format( IColumnContext? context, object? value, IBufferWriter<char> destination )
        {
            ArgumentNullException.ThrowIfNull( destination );
            destination.Write( Format( context, value ).AsSpan() );
        }

        /// <summary>
        ///     Formats a value that supports span formatting straight into the destination, asking for a larger span
        ///     and trying again until the formatted text fits.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        /// <param name="value">The value to format.</param>
        /// <param name="format">The format string, or null for the type's default format.</param>
        /// <param name="provider">The format provider, or null for the current culture.</param>
        protected static void WriteFormatted<TValue>( IBufferWriter<char> destination, TValue value, string? format, IFormatProvider? provider )
            where TValue : ISpanFormattable
        {
            ArgumentNullException.ThrowIfNull( destination );
            var sizeHint = 32;
            while (true)
            {
                var span = destination.GetSpan( sizeHint );
                if (value.TryFormat( span, out var charsWritten, format, provider ))
                {
                    destination.Advance( charsWritten );
                    return;
                }
                sizeHint = span.Length * 2;
            }
        }

        /// <summary>
        ///     Gets the format provider to use. If the given provider is not null, it will be used.
        ///     Otherwise, the format provider set on the options object will be used. As a last resort,
        ///     the current culture specified by the operating system will be used.
        /// </summary>
        /// <param name="context">The current column context.</param>
        /// <param name="formatProvider">The format provider set on the column.</param>
        /// <returns>The format provider to use.</returns>
        protected static IFormatProvider GetFormatProvider( IColumnContext? context, IFormatProvider? formatProvider )
        {
            return formatProvider
                ?? context?.RecordContext.ExecutionContext.Options.FormatProvider
                ?? CultureInfo.CurrentCulture;
        }
    }

    /// <summary>
    ///     Represents the command base class for defining custom column definitions for a class.
    /// </summary>
    /// <typeparam name="T">The type of the column.</typeparam>
    public abstract class ColumnDefinition<T> : ColumnDefinition
    {
        /// <summary>
        ///     Initialises a new instance of a ColumnDefinition.
        /// </summary>
        /// <param name="columnName">The name of the column to define.</param>
        protected ColumnDefinition( string columnName ) 
            : base( columnName )
        {
        }

        /// <summary>
        ///     Gets the type of the values in the column.
        /// </summary>
        public override Type ColumnType => typeof( T );

        /// <summary>
        ///     Parses the given value and returns the parsed object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        public override object? Parse( IColumnContext? context, string value )
        {
            if (OnParsing is not null)
            {
                value = OnParsing( context, value ) ?? string.Empty;
            }
            var result = ParseValue( context, value );
            if (OnParsed is not null)
            {
                result = OnParsed( context, result );
            }
            return result;
        }

        private object? ParseValue( IColumnContext? context, string? value )
        {
            if (value is null || NullFormatter.IsNullValue( context, value ))
            {
                return IsNullable ? null : DefaultValue.GetDefaultValue( context ); // Should we check for the expected type?
            }
            var trimmed = IsTrimmed ? TrimValue( value ) : value;
            return OnParse( context, trimmed );
        }

        /// <summary>
        ///     Parses the given value without the caller first copying it out of the record it sits in. Everything
        ///     the string overload does happens here too; the value is copied only where something needs it as a
        ///     string.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        public override object? Parse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            if (OnParsing is not null || OverridesStringParse)
            {
                // The hook is handed the whole value as a string, and a derived column that replaced the string
                // overload has to keep seeing every value through it.
                return Parse( context, value.ToString() );
            }
            var result = ParseValue( context, value );
            if (OnParsed is not null)
            {
                result = OnParsed( context, result );
            }
            return result;
        }

        private object? ParseValue( IColumnContext? context, ReadOnlySpan<char> value )
        {
            if (NullFormatter.IsNullValue( context, value ))
            {
                return IsNullable ? null : DefaultValue.GetDefaultValue( context );
            }
            return OnParse( context, IsTrimmed ? value.Trim() : value );
        }

        /// <summary>
        ///     Whether the runtime type replaced <see cref="Parse(IColumnContext?, string)" />, in which case the
        ///     value has to reach it as a string. Asked once per column rather than once per value, because the
        ///     answer is a reflection lookup and never changes.
        /// </summary>
        private bool OverridesStringParse => overridesStringParse ??=
            GetType().GetMethod( nameof( Parse ), [typeof( IColumnContext ), typeof( string )] )?.DeclaringType != typeof( ColumnDefinition<T> );

        private bool? overridesStringParse;

        /// <summary>
        ///     Gets whether the value should be trimmed prior to parsing.
        /// </summary>
        protected virtual bool IsTrimmed => true;

        /// <summary>
        ///     Parses the given value and returns the parsed object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        protected abstract T OnParse( IColumnContext? context, string value );

        /// <summary>
        ///     Parses the given value without the caller first copying it out of the record it sits in. The default
        ///     copies the text and parses the string; override it where the column's type can be read from the
        ///     characters themselves.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        protected virtual T OnParse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            return OnParse( context, value.ToString() );
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        public override string Format( IColumnContext? context, object? value )
        {
            if (OnFormatting is not null)
            {
                value = OnFormatting( context, value );
            }
            var result = FormatValue( context, value );
            if (OnFormatted is not null)
            {
                result = OnFormatted( context, result ) ?? string.Empty;
            }
            return result;
        }

        /// <summary>
        ///     Formats the given object and appends the result to a buffer. A null value is written as the null
        ///     formatter's text; anything else goes to <see cref="OnFormat(IColumnContext?, T, IBufferWriter{char})" />.
        ///     When an <see cref="ColumnDefinition.OnFormatted" /> hook is set the value is formatted to a string
        ///     first, because the hook needs the whole string.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        public override void Format( IColumnContext? context, object? value, IBufferWriter<char> destination )
        {
            ArgumentNullException.ThrowIfNull( destination );
            if (OnFormatted is not null)
            {
                destination.Write( Format( context, value ).AsSpan() );
                return;
            }
            if (OnFormatting is not null)
            {
                value = OnFormatting( context, value );
            }
            if (value is null)
            {
                destination.Write( (NullFormatter.FormatNull( context ) ?? string.Empty).AsSpan() );
            }
            else
            {
                OnFormat( context, (T) value, destination );
            }
        }

        private string FormatValue( IColumnContext? context, object? value )
        {
            if (value is null)
            {
                return NullFormatter.FormatNull( context ) ?? string.Empty;
            }
            return OnFormat( context, (T) value );
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        protected abstract string OnFormat( IColumnContext? context, T value );

        /// <summary>
        ///     Formats the given value and appends the result to a buffer. The default writes the string that
        ///     <see cref="OnFormat(IColumnContext?, T)" /> returns; override it to format straight into the buffer.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        protected virtual void OnFormat( IColumnContext? context, T value, IBufferWriter<char> destination )
        {
            destination.Write( OnFormat( context, value ).AsSpan() );
        }
    }
}
