using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using FlatFiles.Properties;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     A constructor that can be handed a record's parsed values, and where in the record each of its
    ///     parameters comes from.
    /// </summary>
    /// <remarks>
    ///     Mapping onto a constructor rather than onto properties is the only way to fill a member that has no
    ///     setter, and the only way to let a type validate what it is given rather than be built empty and
    ///     contradicted afterwards. A positional record is the common case: every one of its properties is
    ///     get-only, and its constructor is the only door in.
    /// </remarks>
    internal sealed class ConstructorMapping
    {
        private ConstructorMapping( ConstructorInfo constructor, int[] logicalIndexes, HashSet<string> filledMembers )
        {
            Constructor = constructor;
            LogicalIndexes = logicalIndexes;
            FilledMembers = filledMembers;
        }

        public ConstructorInfo Constructor { get; }

        /// <summary>
        ///     Where each parameter's value sits among the parsed values, in the constructor's own order.
        /// </summary>
        public int[] LogicalIndexes { get; }

        /// <summary>
        ///     The members the constructor fills, by name. Whatever is in here must not be assigned again
        ///     afterwards: it either cannot be, being read-only, or should not be, having already been given to a
        ///     constructor that may have checked it.
        /// </summary>
        public HashSet<string> FilledMembers { get; }

        /// <summary>
        ///     Finds the constructor to build <paramref name="entityType" /> with, or answers null where the type
        ///     can be built the ordinary way.
        /// </summary>
        /// <remarks>
        ///     Only reached when nothing else can build the type: a caller who supplied a factory has said how to
        ///     build it, and a type with a parameterless constructor is built and then assigned as it always was.
        ///     So this never changes what an existing mapping does - it only gives an answer where the previous
        ///     one was an exception.
        /// </remarks>
        public static ConstructorMapping? Find( Type entityType, IMemberMapping[] mappings )
        {
            if (HasParameterlessConstructor( entityType ))
            {
                return null;
            }
            var members = MappedMembers( mappings );
            // The greediest constructor that can be satisfied wins, so a type offering both a full constructor
            // and a partial one is built as completely as the record allows.
            var candidates = entityType
                .GetConstructors( BindingFlags.Public | BindingFlags.Instance )
                .OrderByDescending( x => x.GetParameters().Length );
            foreach (var candidate in candidates)
            {
                var matched = Match( candidate, members );
                if (matched is not null)
                {
                    return matched;
                }
            }
            throw new FlatFileException( Describe( entityType, members ) );
        }

        private static bool HasParameterlessConstructor( Type entityType )
        {
            return entityType.GetConstructor( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes ) is not null;
        }

        /// <summary>
        ///     The mapped members that could satisfy a constructor parameter, by name. A member reached through a
        ///     parent - a nested entity's own property - is not one of them, since it belongs to something this
        ///     constructor is not building.
        /// </summary>
        private static Dictionary<string, IMemberMapping> MappedMembers( IMemberMapping[] mappings )
        {
            var members = new Dictionary<string, IMemberMapping>( StringComparer.OrdinalIgnoreCase );
            foreach (var mapping in mappings)
            {
                if (mapping.Member is null || mapping.Member.ParentAccessor is not null)
                {
                    continue;
                }
                members[mapping.Member.Name] = mapping;
            }
            return members;
        }

        /// <summary>
        ///     The constructor matched against the mapped members, or null where a parameter has nothing to take
        ///     its value from. Matching is by name, ignoring case, because that is the relationship a positional
        ///     record already establishes between its parameters and its properties.
        /// </summary>
        private static ConstructorMapping? Match( ConstructorInfo candidate, Dictionary<string, IMemberMapping> members )
        {
            // A parameterless candidate cannot arrive here: a type with one of those, public or not, was answered
            // for before any constructor was considered.
            var parameters = candidate.GetParameters();
            var indexes = new int[parameters.Length];
            var filled = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
            for (var index = 0; index != parameters.Length; ++index)
            {
                var parameter = parameters[index];
                if (parameter.Name is null || !members.TryGetValue( parameter.Name, out var mapping ))
                {
                    return null;
                }
                if (!IsCompatible( mapping.Member!.Type, parameter.ParameterType ))
                {
                    return null;
                }
                indexes[index] = mapping.LogicalIndex;
                filled.Add( mapping.Member.Name );
            }
            return new ConstructorMapping( candidate, indexes, filled );
        }

        /// <summary>
        ///     Whether a member's value can be passed as a parameter of the given type. The member's type is what
        ///     the mapping parses to, so the two are normally the same; a parameter declared as a base type or an
        ///     interface is allowed, and a nullable member feeding a non-nullable parameter is not, because the
        ///     value may be null and the constructor has said it will not take one.
        /// </summary>
        private static bool IsCompatible( Type memberType, Type parameterType )
        {
            return parameterType.IsAssignableFrom( memberType );
        }

        private static string Describe( Type entityType, Dictionary<string, IMemberMapping> members )
        {
            var constructors = entityType.GetConstructors( BindingFlags.Public | BindingFlags.Instance );
            var mapped = members.Count == 0
                ? "no members are mapped"
                : "the mapped members are " + string.Join( ", ", members.Values.Select( x => x.Member!.Name ).OrderBy( x => x, StringComparer.Ordinal ) );
            if (constructors.Length == 0)
            {
                return string.Format( CultureInfo.CurrentCulture, Resources.NoConstructorToMapOnto, entityType.Name, "it has no public constructor", mapped );
            }
            var tried = string.Join( "; ", constructors.Select( Describe ) );
            return string.Format( CultureInfo.CurrentCulture, Resources.NoConstructorToMapOnto, entityType.Name, "none of its constructors matched (" + tried + ")", mapped );
        }

        private static string Describe( ConstructorInfo constructor )
        {
            var parameters = constructor.GetParameters().Select( x => x.ParameterType.Name + " " + x.Name );
            // A constructor always belongs to something; it was reached through that type in the first place.
            return constructor.DeclaringType!.Name + "(" + string.Join( ", ", parameters ) + ")";
        }
    }
}
