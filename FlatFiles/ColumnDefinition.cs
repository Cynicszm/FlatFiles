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
            OnParsing is not null || OnParsingSpan is not null || OnParsed is not null || OnFormatting is not null
            || OnFormatted is not null
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
        ///     Gets or sets a hook that transforms a value before it is parsed, reading it where it lies and
        ///     writing its answer into a buffer the reader owns.
        /// </summary>
        /// <remarks>
        ///     The counterpart of <see cref="OnParsing" /> that costs nothing to use, and unlike that one it does
        ///     not stop a column being read straight onto an entity. Where both are set, <see cref="OnParsing" />
        ///     wins and this is not called, because a column that has to produce a string for one hook may as well
        ///     give it to both. See <see cref="SpanParsingHook" />.
        /// </remarks>
        public SpanParsingHook? OnParsingSpan { get; set; }

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
        ///     Sets the format this column parses by, and answers whether it has one.
        /// </summary>
        /// <param name="format">The format to parse by.</param>
        /// <remarks>
        ///     Asked by a mapping built from attributes, where the format arrives as text rather than through the
        ///     mapping interface that knows which column it belongs to. Answered by the column that has one, rather
        ///     than found by reflection over a property name, which a trimmer is free to remove.
        /// </remarks>
        internal virtual bool TrySetInputFormat( string format )
        {
            return false;
        }

        /// <summary>
        ///     Sets the format this column writes by, and answers whether it has one.
        /// </summary>
        /// <param name="format">The format to write by.</param>
        internal virtual bool TrySetOutputFormat( string format )
        {
            return false;
        }

        /// <summary>
        ///     Whether this column ever asks what format provider to use. False for a column that parses and formats
        ///     without one - text, a boolean, a byte array - which is most of them.
        /// </summary>
        /// <remarks>
        ///     The options' provider reaches a column only through a column context, so a column that consults one
        ///     needs a context built for it wherever the options carry a provider. A column that never asks does
        ///     not, and building one for it costs an allocation per column per record for nothing: setting a culture
        ///     on the options used to cost 40 bytes a column on every record, on reading and writing alike, whatever
        ///     the columns were. A column declared outside the library is given a context regardless, by
        ///     <see cref="IsColumnContextRequired" />, because it may read one in its own parsing or formatting.
        /// </remarks>
        internal virtual bool UsesFormatProvider => false;

        /// <summary>
        ///     Whether parsing or formatting a value for this column needs a column context built for it.
        /// </summary>
        /// <param name="definition">The column about to be parsed or formatted.</param>
        /// <param name="options">The options the reader or writer is running with.</param>
        /// <remarks>
        ///     A schema holds its columns as <see cref="IColumnDefinition" />, which anyone may implement, so an
        ///     implementation this class knows nothing about is assumed to want the provider.
        /// </remarks>
        internal static bool NeedsColumnContext( IColumnDefinition definition, IOptions options )
        {
            return definition.IsColumnContextRequired
                || ( options.FormatProvider is not null && ( definition is not ColumnDefinition column || column.UsesFormatProvider ) );
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

        /// <summary>
        ///     Whether this column can be parsed into its own type without going through <see cref="object" />.
        ///     False here, and answered by the column that knows its type.
        /// </summary>
        /// <remarks>
        ///     Declared on this class rather than the generic one so that a mapper, which holds columns as this
        ///     type, can ask without reflection. It used to ask by name through <c>GetProperty</c>, which a trimmer
        ///     is free to remove - and a lookup that comes back empty reads as "no", so the faster path would have
        ///     switched itself off silently in the one configuration it exists for.
        /// </remarks>
        internal virtual bool SupportsTypedParse => false;

        /// <summary>
        ///     Whether a value of this column's own type can be formatted without going through
        ///     <see cref="object" />. False here, and answered by the column that knows its type.
        /// </summary>
        internal virtual bool SupportsTypedFormat => false;

        /// <summary>
        ///     Writes what stands for no value at all, for a member that holds none.
        /// </summary>
        internal void FormatNull( IColumnContext? context, IBufferWriter<char> destination )
        {
            destination.Write( ( NullFormatter.FormatNull( context ) ?? string.Empty ).AsSpan() );
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
        /// <remarks>
        ///     This overload stays its own implementation rather than handing the characters to the span one, for
        ///     two reasons. It is what <see cref="OverridesStringParse" /> routes to, so it cannot route back. And
        ///     most columns override only the string <c>OnParse</c>, whose span counterpart calls it through
        ///     <c>ToString</c> - so going by way of the span would build a string per value for exactly the
        ///     callers that already had one.
        ///     <para>
        ///         What it must not do is decide anything for itself. Every question about nullness, defaults and
        ///         trimming is asked of the same two methods the span overload asks, because the two answering
        ///         differently is a defect this column has shipped twice.
        ///     </para>
        /// </remarks>
        public override object? Parse( IColumnContext? context, string value )
        {
            if (value is null)
            {
                return Parsed( context, NullValue( context, out var missing ), missing );
            }
            if (OnParsing is not null)
            {
                // The string hook wins over the span one: a column that has to build a string for it may as well
                // give it to both.
                var replaced = OnParsing( context, value ) ?? string.Empty;
                return Parsed( context, ParseReplaced( context, replaced, out var hooked ), hooked );
            }
            if (OnParsingSpan is not null)
            {
                // A caller holding a string should get what a reader holding the record would.
                return Parsed( context, ParseTyped( context, value.AsSpan(), out var spanHooked ), spanHooked );
            }
            return Parsed( context, ParseReplaced( context, value, out var parsed ), parsed );
        }

        /// <summary>
        ///     What every parse does once a value has been turned into the column's own type: box it, or box
        ///     nothing where it read as null, and offer it to <see cref="ColumnDefinition.OnParsed" />.
        /// </summary>
        /// <param name="context">Holds information about the column currently being processed.</param>
        /// <param name="hasValue">False where the value read as null.</param>
        /// <param name="parsed">The parsed value, where there is one.</param>
        /// <returns>The value as an object.</returns>
        private object? Parsed( IColumnContext? context, bool hasValue, T parsed )
        {
            object? result = hasValue ? parsed : null;
            return OnParsed is null ? result : OnParsed( context, result );
        }

        /// <summary>
        ///     The string counterpart of <see cref="ParseReplaced(IColumnContext?, ReadOnlySpan{char}, out T)" />,
        ///     which exists only so that a column overriding the string <c>OnParse</c> is reached without the
        ///     value being copied. It asks the same questions in the same order.
        /// </summary>
        /// <param name="context">Holds information about the column currently being processed.</param>
        /// <param name="value">The value, as any hook left it.</param>
        /// <param name="parsed">The parsed value, or the type's default where the value read as null.</param>
        /// <returns>False where the value read as null.</returns>
        private bool ParseReplaced( IColumnContext? context, string value, out T parsed )
        {
            if (NullFormatter.IsNullValue( context, value ))
            {
                return NullValue( context, out parsed );
            }
            parsed = OnParse( context, IsTrimmed ? TrimValue( value ) : value );
            return true;
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
            return Parsed( context, ParseTyped( context, value, out var parsed ), parsed );
        }

        /// <inheritdoc />
        /// <remarks>
        ///     It cannot when anything in the way expects a value of that shape: either of the parsing hooks, which
        ///     are declared in terms of <see cref="object" /> and <see cref="string" />, or a derived column that
        ///     replaced one of the <c>Parse</c> methods and so must see every value itself.
        /// </remarks>
        internal override bool SupportsTypedParse => OnParsing is null && OnParsed is null && !OverridesStringParse && !OverridesSpanParse;

        /// <inheritdoc />
        /// <remarks>
        ///     It cannot when anything in the way expects a value of another shape: either formatting hook, which
        ///     are declared in terms of <see cref="object" /> and <see cref="string" />, or a derived column that
        ///     replaced one of the <c>Format</c> methods and so must see every value itself.
        /// </remarks>
        internal override bool SupportsTypedFormat => OnFormatting is null && OnFormatted is null && !OverridesObjectFormat && !OverridesBufferFormat;

        /// <summary>
        ///     Formats a value of the column's own type straight into the buffer. Only valid where
        ///     <see cref="SupportsTypedFormat" /> says so; what the ordinary path does short of the hooks - the
        ///     null formatter included - happens here too.
        /// </summary>
        /// <param name="context">Holds information about the column currently being processed.</param>
        /// <param name="value">The value to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        internal void FormatTyped( IColumnContext? context, T value, IBufferWriter<char> destination )
        {
            // The guard on the type comes first deliberately. `value is null` alone asks the question of an
            // unconstrained T by boxing it, which costs a box for every value of a value type - the whole thing
            // this path exists to avoid. Asking about the type folds to a constant where T is a value type, and
            // the test that would box is never reached.
            if (!typeof( T ).IsValueType && value is null)
            {
                FormatNull( context, destination );
                return;
            }
            OnFormat( context, value, destination );
        }

        /// <summary>
        ///     Whether the runtime type replaced <see cref="Format(IColumnContext?, object?)" />, in which case
        ///     every value has to reach it as an object.
        /// </summary>
        private bool OverridesObjectFormat => overridesObjectFormat ??=
            GetType().GetMethod( nameof( Format ), [typeof( IColumnContext ), typeof( object )] )?.DeclaringType != typeof( ColumnDefinition<T> );

        private bool? overridesObjectFormat;

        /// <summary>
        ///     Whether the runtime type replaced <see cref="Format(IColumnContext?, object?, IBufferWriter{char})" />.
        ///     Asked once per column rather than once per value, because the answer is a reflection lookup and
        ///     never changes.
        /// </summary>
        private bool OverridesBufferFormat => overridesBufferFormat ??=
            GetType().GetMethod( nameof( Format ), [typeof( IColumnContext ), typeof( object ), typeof( IBufferWriter<char> )] )?.DeclaringType != typeof( ColumnDefinition<T> );

        private bool? overridesBufferFormat;

        /// <summary>
        ///     Parses the value into the column's own type rather than into an object. Only valid where
        ///     <see cref="SupportsTypedParse" /> says so; everything the ordinary parse does short of the hooks
        ///     happens here too, so a null formatter, a default value and trimming all still apply.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <param name="parsed">The parsed value, or the type's default when the value reads as null.</param>
        /// <returns>False when the value reads as null, in which case the caller decides what null means.</returns>
        internal bool ParseTyped( IColumnContext? context, ReadOnlySpan<char> value, out T parsed )
        {
            if (OnParsingSpan is null)
            {
                return ParseReplaced( context, value, out parsed );
            }
            // The hook first, then everything else applied to what it answered - the order the object path uses.
            // The other way round, a blank value never reaches the hook at all, and whatever the hook wrote is
            // parsed without anyone asking whether it reads as null.
            var replaced = Replaced( context, value, out var rented );
            try
            {
                return ParseReplaced( context, replaced, out parsed );
            }
            finally
            {
                Release( rented );
            }
        }

        /// <summary>
        ///     Everything a parse does to a value once any hook has had it: the null formatter, a default value
        ///     where the column cannot hold null, and the trim. Every overload arrives here.
        /// </summary>
        /// <param name="context">Holds information about the column currently being processed.</param>
        /// <param name="value">The value, as the hook left it.</param>
        /// <param name="parsed">The parsed value, or the type's default when the value reads as null.</param>
        /// <returns>False when the value reads as null, in which case the caller decides what null means.</returns>
        private bool ParseReplaced( IColumnContext? context, ReadOnlySpan<char> value, out T parsed )
        {
            if (NullFormatter.IsNullValue( context, value ))
            {
                return NullValue( context, out parsed );
            }
            parsed = OnParse( context, IsTrimmed ? value.Trim() : value );
            return true;
        }

        /// <summary>
        ///     What a value that reads as null becomes: nothing where the column can hold null, and otherwise the
        ///     default value, which may itself be nothing.
        /// </summary>
        /// <param name="context">Holds information about the column currently being processed.</param>
        /// <param name="parsed">The default value, where there is one.</param>
        /// <returns>False where the answer is null.</returns>
        private bool NullValue( IColumnContext? context, out T parsed )
        {
            if (IsNullable)
            {
                parsed = default!;
                return false;
            }
            var substitute = DefaultValue.GetDefaultValue( context );
            if (substitute is null)
            {
                parsed = default!;
                return false;
            }
            parsed = (T) substitute;
            return true;
        }

        /// <summary>
        ///     Runs <see cref="ColumnDefinition.OnParsingSpan" /> and gives back the value to parse: the one that was passed in
        ///     where the hook asked for nothing, and otherwise what it wrote.
        /// </summary>
        /// <param name="context">Holds information about the column being processed.</param>
        /// <param name="value">The value as it lies in the record.</param>
        /// <param name="rented">The array the answer was written to, which the caller returns, or null.</param>
        /// <returns>The value to parse, valid until <paramref name="rented" /> is released.</returns>
        /// <remarks>
        ///     The first attempt writes into an array rented at the length a value of this size could plausibly
        ///     need. A hook wanting more says so by returning the negation of the length it needs, and is asked
        ///     again with an array at least that long. Nothing is allocated either way: the pool is where these
        ///     come from and the caller returns them.
        /// </remarks>
        private ReadOnlySpan<char> Replaced( IColumnContext? context, ReadOnlySpan<char> value, out char[]? rented )
        {
            var hook = OnParsingSpan!;
            rented = ArrayPool<char>.Shared.Rent( value.Length + SpareRoom );
            var written = hook( context, value, rented );
            if (IsRequest( written ))
            {
                var wanted = -written;
                ArrayPool<char>.Shared.Return( rented );
                rented = ArrayPool<char>.Shared.Rent( wanted );
                written = hook( context, value, rented.AsSpan( 0, wanted ) );
                if (IsRequest( written ))
                {
                    var message = string.Format( CultureInfo.CurrentCulture, Resources.SpanParsingHookAskedTwice, ColumnName, wanted );
                    Release( rented );
                    rented = null;
                    throw new FlatFileException( message );
                }
            }
            if (written == SpanParsingHooks.Unchanged)
            {
                Release( rented );
                rented = null;
                return value;
            }
            return rented.AsSpan( 0, written );
        }

        /// <summary>
        ///     Whether what a hook returned is asking for a longer buffer, rather than a length it wrote or a
        ///     wish to be left alone.
        /// </summary>
        /// <param name="written">What the hook returned.</param>
        /// <returns>True where it is asking for room.</returns>
        /// <remarks>
        ///     Anything negative other than <see cref="SpanParsingHooks.Unchanged" />, which is
        ///     <see cref="int.MinValue" /> so that no length a hook can ask for is mistaken for it.
        /// </remarks>
        private static bool IsRequest( int written )
        {
            return written < 0 && written != SpanParsingHooks.Unchanged;
        }

        /// <summary>Returns a rented array, if one was taken.</summary>
        /// <param name="rented">The array, or null.</param>
        private static void Release( char[]? rented )
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return( rented );
            }
        }

        /// <summary>
        ///     How much longer than the value a replacement is assumed to be before the hook has to ask for more.
        ///     A hook that rewrites a value usually returns something close to its own length - a sign, a decimal
        ///     point, a stripped symbol - so asking twice should be rare.
        /// </summary>
        private const int SpareRoom = SpanParsingHooks.MinimumOffered;

        /// <summary>
        ///     Whether the runtime type replaced <see cref="Parse(IColumnContext?, ReadOnlySpan{char})" />.
        /// </summary>
        private bool OverridesSpanParse => overridesSpanParse ??=
            GetType().GetMethod( nameof( Parse ), [typeof( IColumnContext ), typeof( ReadOnlySpan<char> )] )?.DeclaringType != typeof( ColumnDefinition<T> );

        private bool? overridesSpanParse;

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
