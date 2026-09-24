using System;
using System.Collections.Concurrent;
using FlatFiles.TypeMapping;

namespace FlatFiles.CodeGeneration
{
    /// <summary>
    ///     Where code written at compile time hands the library the accessors it would otherwise have to build at
    ///     run time, so that a mapping reads onto an entity without any value being boxed on the way even where
    ///     the runtime cannot generate code.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A type mapper usually builds those accessors itself, by closing <see cref="ColumnSetter{TEntity,T}" />
    ///         over the column's type. Doing that needs <see cref="Type.MakeGenericType" /> and a delegate made
    ///         from a <see cref="System.Reflection.MethodInfo" />, neither of which a runtime without dynamic code
    ///         can do for a value type it was not built with. Under Native AOT the mapper therefore falls back to
    ///         reflection and a box for every value - and this is how that is avoided.
    ///     </para>
    ///     <para>
    ///         Every registration closes its own generic types where it is written, which is the whole mechanism:
    ///         nothing is closed later, so nothing has to be closed at run time. Measured on 10,000 records of 13
    ///         columns with the dynamic-code switch off, a delimited read through a type mapper allocates 622 bytes
    ///         a record unregistered and 359 registered, which is what the same read costs with the switch on.
    ///     </para>
    ///     <para>
    ///         Registering nothing changes nothing: a mapping nothing covers builds its accessors as it always
    ///         did. So does a mapping registered against a column of a different type, which is refused here
    ///         rather than failing part way through a read.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     Registering an entity's members, which is what generated code does from a module initialiser:
    ///     <code>
    ///     MappingAccessors.AddFactory( () =&gt; new Customer() );
    ///     MappingAccessors.AddSetter&lt;Customer, int&gt;( "CustomerId", ( e, v ) =&gt; e.CustomerId = v );
    ///     MappingAccessors.AddSetter&lt;Customer, string&gt;( "Name", ( e, v ) =&gt; e.Name = v );
    ///     </code>
    /// </example>
    public static class MappingAccessors
    {
        private static readonly ConcurrentDictionary<Registration, Accessor> setters = new();

        private static readonly ConcurrentDictionary<Type, object> factories = new();

        /// <summary>
        ///     Registers how an entity is made, in place of the one the library would generate.
        /// </summary>
        /// <typeparam name="TEntity">The type being built.</typeparam>
        /// <param name="factory">Makes one.</param>
        /// <exception cref="ArgumentNullException">The factory is null.</exception>
        /// <remarks>A later registration for the same type replaces an earlier one.</remarks>
        public static void AddFactory<TEntity>( Func<TEntity> factory )
        {
            ArgumentNullException.ThrowIfNull( factory );
            factories[typeof( TEntity )] = factory;
        }

        /// <summary>
        ///     Registers how a member is set from a column of the same type.
        /// </summary>
        /// <typeparam name="TEntity">The type declaring the member.</typeparam>
        /// <typeparam name="T">The type the column parses to and the member holds.</typeparam>
        /// <param name="member">The member's name.</param>
        /// <param name="assign">Sets the member.</param>
        /// <exception cref="ArgumentNullException">The name or the delegate is null.</exception>
        /// <remarks>
        ///     <typeparamref name="TEntity" /> is the type that declares the member, which is not always the type
        ///     being mapped: a member declared on a base class is registered against the base. A later
        ///     registration for the same member replaces an earlier one.
        /// </remarks>
        public static void AddSetter<TEntity, T>( string member, Action<TEntity, T> assign )
        {
            ArgumentNullException.ThrowIfNull( member );
            ArgumentNullException.ThrowIfNull( assign );
            setters[new Registration( typeof( TEntity ), member )] = new Accessor( typeof( T ), false,
                ( definition, accessor ) => new ColumnSetter<TEntity, T>( (ColumnDefinition<T>) definition, assign, accessor ) );
        }

        /// <summary>
        ///     Registers how a member is set from a column whose type it holds the nullable form of.
        /// </summary>
        /// <typeparam name="TEntity">The type declaring the member.</typeparam>
        /// <typeparam name="T">The type the column parses to.</typeparam>
        /// <param name="member">The member's name.</param>
        /// <param name="assign">Sets the member.</param>
        /// <exception cref="ArgumentNullException">The name or the delegate is null.</exception>
        /// <remarks>
        ///     For a member of type <c>T?</c> fed by a column of type <c>T</c>, which is how an empty field
        ///     reaches a member that can hold the absence of a value.
        /// </remarks>
        public static void AddNullableSetter<TEntity, T>( string member, Action<TEntity, T?> assign )
            where T : struct
        {
            ArgumentNullException.ThrowIfNull( member );
            ArgumentNullException.ThrowIfNull( assign );
            setters[new Registration( typeof( TEntity ), member )] = new Accessor( typeof( T ), true,
                ( definition, _ ) => new NullableColumnSetter<TEntity, T>( (ColumnDefinition<T>) definition, assign ) );
        }

        internal static bool Covers( Type entity )
        {
            foreach (var registration in setters.Keys)
            {
                if (registration.Declaring.IsAssignableFrom( entity ))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        ///     The setter registered for a member, or null where none is or where the one registered does not
        ///     describe this column.
        /// </summary>
        internal static IColumnSetter<TEntity>? Setter<TEntity>( Type declaring, string member, ColumnDefinition definition, Type columnType, bool isNullable, IMemberAccessor accessor )
        {
            if (!setters.TryGetValue( new Registration( declaring, member ), out var registered ))
            {
                return null;
            }
            // A mapping is free to put a column on a member that a registration was not written for. Saying no
            // here leaves the mapping to build its own accessor, which is what it would have done anyway.
            if (registered.ColumnType != columnType || registered.IsNullable != isNullable)
            {
                return null;
            }
            return registered.Build( definition, accessor ) as IColumnSetter<TEntity>;
        }

        internal static Func<TEntity>? Factory<TEntity>()
        {
            return factories.TryGetValue( typeof( TEntity ), out var factory ) ? factory as Func<TEntity> : null;
        }

        /// <summary>
        ///     A member is named by the type that declares it, so that a property hiding one of the same name on a
        ///     base class is a registration of its own rather than a collision.
        /// </summary>
        private readonly record struct Registration( Type Declaring, string Member );

        /// <summary>
        ///     What was registered: the column type it was written for, and how to make the setter.
        /// </summary>
        private readonly record struct Accessor( Type ColumnType, bool IsNullable, Func<ColumnDefinition, IMemberAccessor, object> Build );
    }
}
