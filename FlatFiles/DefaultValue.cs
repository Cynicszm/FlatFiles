using System;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Generates a default value whenever a null is encountered on a non-nullable column.
    /// </summary>
    public sealed class DefaultValue : IDefaultValue
    {
        private readonly Func<IColumnContext?, object?> valueFactory;

        private DefaultValue( Func<IColumnContext?, object?> factory, bool usesColumnContext )
        {
            valueFactory = factory;
            UsesColumnContext = usesColumnContext;
        }

        /// <summary>
        ///     Whether the default value may read the column context it is given: true for a caller's delegate, false
        ///     for a fixed value and for the disabled default, which never look at it.
        /// </summary>
        internal bool UsesColumnContext { get; }

        /// <summary>
        ///     Use the given value as a default.
        /// </summary>
        /// <param name="value">The value to use as a default.</param>
        /// <returns>An instance of a <see cref="IDefaultValue"/> that returns the given value.</returns>
        public static IDefaultValue Use( object? value )
        {
            return new DefaultValue( _ => value, false );
        }

        /// <summary>
        ///     Use the given delegate to generate the default value.
        /// </summary>
        /// <param name="factory">The value to use as a default.</param>
        /// <returns>An instance of a <see cref="IDefaultValue"/> that returns the result of the delegate.</returns>
        public static IDefaultValue Use( Func<IColumnContext?, object?> factory )
        {
            ArgumentNullException.ThrowIfNull( factory );
            return new DefaultValue( factory, true );
        }

        /// <summary>
        ///     Throw an exception when an unexpected null is encountered.
        /// </summary>
        /// <returns>An instance of a <see cref="IDefaultValue"/> that throws an exception.</returns>
        public static IDefaultValue Disabled()
        {
            return new DefaultValue( _ => throw new InvalidCastException( Resources.AssignNullToNonNullable ), false );
        }

        /// <summary>
        ///     Gets the default value to use when a null is encountered on a non-nullable column.
        /// </summary>
        /// <param name="context">The current column context.</param>
        /// <returns>The default value.</returns>
        public object? GetDefaultValue( IColumnContext? context )
        {
            return valueFactory( context );
        }
    }
}
