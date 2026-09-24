using System;
using System.Collections.Immutable;

namespace FlatFiles.Generator
{
    /// <summary>
    ///     A type something in the compilation asked to be mapped, with what can be written for it.
    /// </summary>
    internal readonly struct MappedEntity : IEquatable<MappedEntity>
    {
        public MappedEntity( string qualifiedName, string hintName, bool canBeConstructed, ImmutableArray<MappedMember> members )
        {
            QualifiedName = qualifiedName;
            HintName = hintName;
            CanBeConstructed = canBeConstructed;
            Members = members;
        }

        /// <summary>
        ///     The type as generated code must spell it, global alias and all.
        /// </summary>
        public string QualifiedName { get; }

        /// <summary>
        ///     Something unique and legal to name the generated file and class after.
        /// </summary>
        public string HintName { get; }

        /// <summary>
        ///     Whether the entity has a parameterless constructor the generated code can reach. Without one there
        ///     is no factory to register, and a mapping builds the entity the way it always did.
        /// </summary>
        public bool CanBeConstructed { get; }

        /// <summary>
        ///     Every property considered, whether or not one could be written for.
        /// </summary>
        public ImmutableArray<MappedMember> Members { get; }

        public bool Equals( MappedEntity other )
        {
            if (QualifiedName != other.QualifiedName || CanBeConstructed != other.CanBeConstructed)
            {
                return false;
            }
            if (Members.Length != other.Members.Length)
            {
                return false;
            }
            // By value, and in order: an ImmutableArray compares by reference, which would tell the compiler
            // every run differs and throw away what it had cached.
            for (var index = 0; index != Members.Length; ++index)
            {
                if (!Members[index].Equals( other.Members[index] ))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals( object? obj )
        {
            return obj is MappedEntity other && Equals( other );
        }

        public override int GetHashCode()
        {
            var hash = QualifiedName?.GetHashCode() ?? 0;
            hash = ( hash * 397 ) ^ ( CanBeConstructed ? 1 : 0 );
            foreach (var member in Members)
            {
                hash = ( hash * 397 ) ^ member.GetHashCode();
            }
            return hash;
        }
    }
}
