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
            var currentType = entityType;
            var accessor = parent;
            for (var index = nameIndex; index != memberNames.Length; ++index)
            {
                var memberName = memberNames[index];
                var propertyInfo = GetProperty( currentType, memberName );
                if (propertyInfo is not null)
                {
                    accessor = new PropertyAccessor( propertyInfo, accessor );
                    currentType = propertyInfo.PropertyType;
                    continue;
                }
                var fieldInfo = GetField( currentType, memberName ) ?? throw new ArgumentException( Resources.BadPropertySelector, nameof( memberNames ) );
                accessor = new FieldAccessor( fieldInfo, accessor );
                currentType = fieldInfo.FieldType;
            }
            return accessor;
        }

        private static PropertyInfo? GetProperty( Type type, string propertyName )
        {
            const BindingFlags bindingFlags = BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            return type.GetTypeInfo().GetProperty( propertyName, bindingFlags );
        }

        private static FieldInfo? GetField( Type type, string fieldName )
        {
            const BindingFlags bindingFlags = BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
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
            var declaredOnEntity = member.Member.DeclaringType!.GetTypeInfo().IsAssignableFrom( typeof( TEntity ) );
            if (!declaredOnEntity && member.Expression is null)
            {
                // A nested member needs an instance to read from. A static member has none, and
                // recursing on the null would surface as a NullReferenceException from inside.
                throw new ArgumentException( Resources.BadPropertySelector, nameof( expression ) );
            }
            var parentAccessor = declaredOnEntity ? null : GetMember<TEntity>( member.Expression! );
            return member.Member switch
            {
                PropertyInfo propertyInfo => new PropertyAccessor( propertyInfo, parentAccessor ),
                FieldInfo fieldInfo => new FieldAccessor( fieldInfo, parentAccessor ),
                _ => throw new ArgumentException( Resources.BadPropertySelector, nameof( expression ) )
            };
        }

        public static ConstructorInfo? GetConstructor<TEntity>( params Type[] parameterTypes )
        {
            const BindingFlags bindingFlags = BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
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

        public static MemberInfo? GetMemberInfo<TEntity, TValue>( Expression<Func<TEntity, TValue>> accessor )
        {
            return accessor.Body is MemberExpression member ? member.Member : null;
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
