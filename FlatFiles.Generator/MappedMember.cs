using System;

namespace FlatFiles.Generator
{
    /// <summary>
    ///     A property of a mapped entity, and either how to write a setter for it or why nothing can be.
    /// </summary>
    internal readonly struct MappedMember : IEquatable<MappedMember>
    {
        private MappedMember( string name, string valueType, bool isNullable, string? refusal, SourcePosition position )
        {
            Name = name;
            ValueType = valueType;
            IsNullable = isNullable;
            Refusal = refusal;
            Position = position;
        }

        public static MappedMember Writable( string name, string valueType, bool isNullable )
        {
            return new MappedMember( name, valueType, isNullable, null, default );
        }

        public static MappedMember Refused( string name, string refusal, SourcePosition position )
        {
            return new MappedMember( name, string.Empty, false, refusal, position );
        }

        public string Name { get; }

        /// <summary>
        ///     The type a column would have to parse to. For a nullable member that is the underlying type, since
        ///     that is what the column answers and what the registration is written in terms of.
        /// </summary>
        public string ValueType { get; }

        public bool IsNullable { get; }

        /// <summary>
        ///     Why no setter can be written, or null where one can.
        /// </summary>
        public string? Refusal { get; }

        /// <summary>
        ///     Where to point when saying so.
        /// </summary>
        public SourcePosition Position { get; }

        public bool Equals( MappedMember other )
        {
            return Name == other.Name
                && ValueType == other.ValueType
                && IsNullable == other.IsNullable
                && Refusal == other.Refusal
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
            return ( hash * 397 ) ^ ( Refusal?.GetHashCode() ?? 0 );
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
