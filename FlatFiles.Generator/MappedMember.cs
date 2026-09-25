using System;

namespace FlatFiles.Generator
{
    /// <summary>
    ///     A property of a mapped entity: what can be written for it in each direction, and why anything that
    ///     cannot be.
    /// </summary>
    /// <remarks>
    ///     The two directions are asked separately because a property need not allow both. A get-only property is
    ///     written from and not read onto; one with a private setter is the same. Whichever direction is available
    ///     is worth having, and the other is said aloud rather than left to be noticed in a profile.
    /// </remarks>
    internal readonly struct MappedMember : IEquatable<MappedMember>
    {
        private MappedMember( string name, string valueType, bool isNullable, bool canSet, bool canGet, string? setterRefusal, string? getterRefusal, SourcePosition position )
        {
            Name = name;
            ValueType = valueType;
            IsNullable = isNullable;
            CanSet = canSet;
            CanGet = canGet;
            SetterRefusal = setterRefusal;
            GetterRefusal = getterRefusal;
            Position = position;
        }

        public static MappedMember Accessible( string name, string valueType, bool isNullable, bool canSet, bool canGet, string? setterRefusal, string? getterRefusal, SourcePosition position )
        {
            return new MappedMember( name, valueType, isNullable, canSet, canGet, setterRefusal, getterRefusal, position );
        }

        /// <summary>
        ///     Nothing can be written for this member in either direction, for one reason that covers both.
        /// </summary>
        public static MappedMember Refused( string name, string refusal, SourcePosition position )
        {
            return new MappedMember( name, string.Empty, false, false, false, refusal, null, position );
        }

        public string Name { get; }

        /// <summary>
        ///     The type a column would have to read or write. For a nullable member that is the underlying type,
        ///     since that is what the column deals in and what the registration is written in terms of.
        /// </summary>
        public string ValueType { get; }

        public bool IsNullable { get; }

        public bool CanSet { get; }

        public bool CanGet { get; }

        /// <summary>
        ///     Why this member cannot be read onto, or null where it can.
        /// </summary>
        public string? SetterRefusal { get; }

        /// <summary>
        ///     Why this member cannot be written from, or null where it can.
        /// </summary>
        public string? GetterRefusal { get; }

        /// <summary>
        ///     Where to point when saying so.
        /// </summary>
        public SourcePosition Position { get; }

        public bool Equals( MappedMember other )
        {
            return Name == other.Name
                && ValueType == other.ValueType
                && IsNullable == other.IsNullable
                && CanSet == other.CanSet
                && CanGet == other.CanGet
                && SetterRefusal == other.SetterRefusal
                && GetterRefusal == other.GetterRefusal
                && Position.Equals( other.Position );
        }

        public override bool Equals( object? obj )
        {
            return obj is MappedMember other && Equals( other );
        }

        public override int GetHashCode()
        {
            var hash = Name?.GetHashCode() ?? 0;
            hash = ( hash * 397 ) ^ ( ValueType?.GetHashCode() ?? 0 );
            hash = ( hash * 397 ) ^ ( IsNullable ? 1 : 0 );
            hash = ( hash * 397 ) ^ ( CanSet ? 2 : 0 );
            hash = ( hash * 397 ) ^ ( CanGet ? 4 : 0 );
            hash = ( hash * 397 ) ^ ( SetterRefusal?.GetHashCode() ?? 0 );
            return ( hash * 397 ) ^ ( GetterRefusal?.GetHashCode() ?? 0 );
        }
    }

    /// <summary>
    ///     Where something is written, kept as values rather than as a <see cref="Microsoft.CodeAnalysis.Location" />
    ///     so that two runs over the same source compare equal and the compiler can reuse what it has.
    /// </summary>
    internal readonly struct SourcePosition : IEquatable<SourcePosition>
    {
        public SourcePosition( string filePath, int start, int length, int startLine, int startCharacter, int endLine, int endCharacter )
        {
            FilePath = filePath;
            Start = start;
            Length = length;
            StartLine = startLine;
            StartCharacter = startCharacter;
            EndLine = endLine;
            EndCharacter = endCharacter;
        }

        public string? FilePath { get; }

        public int Start { get; }

        public int Length { get; }

        public int StartLine { get; }

        public int StartCharacter { get; }

        public int EndLine { get; }

        public int EndCharacter { get; }

        public bool Equals( SourcePosition other )
        {
            return FilePath == other.FilePath && Start == other.Start && Length == other.Length;
        }

        public override bool Equals( object? obj )
        {
            return obj is SourcePosition other && Equals( other );
        }

        public override int GetHashCode()
        {
            var hash = FilePath?.GetHashCode() ?? 0;
            hash = ( hash * 397 ) ^ Start;
            return ( hash * 397 ) ^ Length;
        }
    }
}
