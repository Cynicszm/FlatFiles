using System;
using Microsoft.CodeAnalysis;

namespace FlatFiles.Generator
{
    /// <summary>
    ///     A type something in the compilation asked to be mapped, and whether anything can be written for it.
    /// </summary>
    internal readonly struct MappedEntity : IEquatable<MappedEntity>
    {
        public MappedEntity( string qualifiedName, string hintName, bool canBeConstructed )
        {
            QualifiedName = qualifiedName;
            HintName = hintName;
            CanBeConstructed = canBeConstructed;
        }

        /// <summary>
        ///     The type as generated code must spell it, global alias and all.
        /// </summary>
        public string QualifiedName { get; }

        /// <summary>
        ///     Something unique and legal to name the generated file after.
        /// </summary>
        public string HintName { get; }

        /// <summary>
        ///     Whether the entity has a parameterless constructor the generated code can reach. Without one there
        ///     is no factory to register, and the mapping builds the entity the way it always did.
        /// </summary>
        public bool CanBeConstructed { get; }

        public bool Equals( MappedEntity other )
        {
            return QualifiedName == other.QualifiedName && CanBeConstructed == other.CanBeConstructed;
        }

        public override bool Equals( object? obj )
        {
            return obj is MappedEntity other && Equals( other );
        }

        public override int GetHashCode()
        {
            // Combined by hand: netstandard2.0 has no HashCode.Combine.
            var hash = QualifiedName?.GetHashCode() ?? 0;
            return ( hash * 397 ) ^ ( CanBeConstructed ? 1 : 0 );
        }
    }
}
