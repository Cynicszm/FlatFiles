using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Whether emitted code can see a type. The generated deserialiser lives in an assembly of its own, so a
    ///     type the rest of the world cannot see is one it cannot construct or assign to either, and the failure
    ///     is a <see cref="MethodAccessException" /> raised from generated code on the first record rather than
    ///     anything the caller can act on. A mapping onto such a type is read reflectively instead, which is what
    ///     a type keeping its constructor to itself has always done.
    /// </summary>
    /// <remarks>
    ///     An assembly may let the generated code in, by naming <c>FlatFiles.DynamicAssembly</c> in an
    ///     <see cref="InternalsVisibleToAttribute" />, and one that has is taken at its word - its internal types
    ///     keep the faster path. This library's own test assembly does exactly that, which is why an internal
    ///     entity worked there while failing everywhere else.
    /// </remarks>
    internal static class TypeVisibility
    {
        private const string DynamicAssemblyName = "FlatFiles.DynamicAssembly";

        private static readonly ConcurrentDictionary<Assembly, bool> opened = new();

        public static bool IsAccessible( Type type )
        {
            if (type.IsGenericType && !Array.TrueForAll( type.GetGenericArguments(), IsAccessible ))
            {
                return false;
            }
            for (var current = type; current is not null; current = current.DeclaringType)
            {
                if (!current.IsNested)
                {
                    return current.IsPublic || LetsTheGeneratedCodeIn( current.Assembly );
                }
                // A private or protected nesting is out of reach whatever the assembly says, since what an
                // assembly can open up is what it keeps internal.
                if (current.IsNestedPrivate || current.IsNestedFamily || current.IsNestedFamANDAssem)
                {
                    return false;
                }
                if (!current.IsNestedPublic && !LetsTheGeneratedCodeIn( current.Assembly ))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool LetsTheGeneratedCodeIn( Assembly assembly )
        {
            return opened.GetOrAdd( assembly, Names );
        }

        private static bool Names( Assembly assembly )
        {
            foreach (var attribute in assembly.GetCustomAttributes<InternalsVisibleToAttribute>())
            {
                var name = attribute.AssemblyName;
                var comma = name.IndexOf( ',' );
                var bare = comma < 0 ? name : name[..comma];
                if (bare.Trim().Equals( DynamicAssemblyName, StringComparison.OrdinalIgnoreCase ))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
