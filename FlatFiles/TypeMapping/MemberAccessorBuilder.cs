using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FlatFiles.Properties;

namespace FlatFiles.TypeMapping
{
    internal static class MemberAccessorBuilder
    {
        public static IMemberAccessor? GetMember<TEntity, TProp>( string memberName )
        {
            return GetMember<TEntity>( typeof( TProp ), memberName );
        }

        public static IMemberAccessor? GetMember<TEntity>( Type propertyType, string memberName )
        {
            var memberNames = memberName.Split( '.' );
            var member = GetMember( typeof( TEntity ), memberNames, 0, null );
            if (member is not null
                && member.Type != propertyType 
                && member.Type != Nullable.GetUnderlyingType( propertyType ))
            {
                throw new ArgumentException( Resources.WrongPropertyType );
            }
            return member;
        }

        public static IMemberAccessor? GetMember( Type entityType, string[] memberNames, int nameIndex, IMemberAccessor? parent )
        {
            if (nameIndex == memberNames.Length)
            {
                return parent;
            }
            var memberName = memberNames[nameIndex];
            var propertyInfo = GetProperty( entityType, memberName );
            if (propertyInfo is not null)
            {
                var accessor = new PropertyAccessor( propertyInfo, parent );
                return GetMember( propertyInfo.PropertyType, memberNames, nameIndex + 1, accessor );
            }
            var fieldInfo = GetField( entityType, memberName );
            if (fieldInfo is not null)
            {
                var accessor = new FieldAccessor( fieldInfo, parent );
                return GetMember( fieldInfo.FieldType, memberNames, nameIndex + 1, accessor );
            }
            throw new ArgumentException( Resources.BadPropertySelector, nameof( memberName ) );
        }

        private static PropertyInfo? GetProperty( Type type, string propertyName )
        {
            var bindingFlags = BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            return type.GetTypeInfo().GetProperty( propertyName, bindingFlags );
        }

        private static FieldInfo? GetField( Type type, string fieldName )
        {
            var bindingFlags = BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            return type.GetTypeInfo().GetField( fieldName, bindingFlags );
        }

        public static IMemberAccessor GetMember<TEntity, TProp>( Expression<Func<TEntity, TProp>> accessor )
        {
            ArgumentNullException.ThrowIfNull( accessor );
            return GetMember<TEntity>( accessor.Body );
        }

        private static IMemberAccessor GetMember<TEntity>( Expression expression )
        {
            if (expression is not MemberExpression member)
            {
                throw new ArgumentException( Resources.BadPropertySelector, nameof( expression ) );
            }
            if (member.Member is PropertyInfo propertyInfo)
            {
                if (propertyInfo.DeclaringType!.GetTypeInfo().IsAssignableFrom( typeof( TEntity ) ))
                {
                    return new PropertyAccessor( propertyInfo, null );
                }

                if (member.Expression is null)
                {
                    // A nested member needs an instance to read from. A static member has none, and
                    // recursing on the null would surface as a NullReferenceException from inside.
                    throw new ArgumentException( Resources.BadPropertySelector, nameof( expression ) );
                }
                var parentAccessor = GetMember<TEntity>( member.Expression );
                return new PropertyAccessor( propertyInfo, parentAccessor );
            }

            if (member.Member is FieldInfo fieldInfo)
            {
                if (fieldInfo.DeclaringType!.GetTypeInfo().IsAssignableFrom( typeof( TEntity ) ))
                {
                    return new FieldAccessor( fieldInfo, null );
                }

                if (member.Expression is null)
                {
                    // A nested member needs an instance to read from. A static member has none, and
                    // recursing on the null would surface as a NullReferenceException from inside.
                    throw new ArgumentException( Resources.BadPropertySelector, nameof( expression ) );
                }
                var parentAccessor = GetMember<TEntity>( member.Expression );
                return new FieldAccessor( fieldInfo, parentAccessor );
            }

            throw new ArgumentException( Resources.BadPropertySelector, nameof( expression ) );
        }

        public static ConstructorInfo? GetConstructor<TEntity>( params Type[] parameterTypes )
        {
            var bindingFlags = BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var query = from constructor in typeof( TEntity ).GetTypeInfo().GetConstructors( bindingFlags )
                        let parameters = constructor.GetParameters()
                        where parameters.Length == parameterTypes.Length
                        let actualParameterTypes = parameters.Select( p => p.ParameterType ).ToArray()
                        where HaveMatchingTypes( parameterTypes, actualParameterTypes )
                        select constructor;
            var constructors = query.ToArray();
            return constructors.Length == 1 ? constructors[0] : null;
        }

        private static bool HaveMatchingTypes( Type[] expected, Type[] actual )
        {
            for (var index = 0; index != expected.Length; ++index)
            {
                if (expected[index] != actual[index])
                {
                    return false;
                }
            }
            return true;
        }

        public static PropertyInfo? GetProperty<TEntity, TProp>( Expression<Func<TEntity, TProp>> accessor )
        {
            var memberInfo = GetMemberInfo( accessor );
            if (memberInfo is not PropertyInfo propertyInfo)
            {
                return null;
            }
            return propertyInfo.DeclaringType!.GetTypeInfo().IsAssignableFrom( typeof( TEntity ) ) ? propertyInfo : null;
        }

        public static FieldInfo? GetField<TEntity, TValue>( Expression<Func<TEntity, TValue>> accessor )
        {
            var memberInfo = GetMemberInfo( accessor );
            if (memberInfo is not FieldInfo fieldInfo) 
            {
                return null;
            }
            return fieldInfo.DeclaringType!.GetTypeInfo().IsAssignableFrom( typeof( TEntity ) ) ? fieldInfo : null;
        }

        public static MemberInfo? GetMemberInfo<TEntity, TValue>( Expression<Func<TEntity, TValue>> accessor )
        {
            return accessor.Body is MemberExpression member ? member.Member : null;
        }

        public static MethodInfo? GetMethod<TEntity, TReturn>( Expression<Func<TEntity, TReturn>> accessor )
        {
            if (accessor.Body is not MethodCallExpression method)
            {
                return null;
            }
            return method.Method.DeclaringType!.GetTypeInfo().IsAssignableFrom( typeof( TEntity ) ) ? method.Method : null;
        }

        public static MethodInfo? GetMethod<TEntity>( Expression<Action<TEntity>> accessor )
        {
            if (accessor.Body is not MethodCallExpression method)
            {
                return null;
            }
            return method.Method.DeclaringType!.GetTypeInfo().IsAssignableFrom( typeof( TEntity ) ) ? method.Method : null;
        }
    }
}
