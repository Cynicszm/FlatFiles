using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FlatFiles.Generator
{
    /// <summary>
    ///     Writes the accessor registrations a mapping would otherwise have to build at run time, so that a type
    ///     mapper reads onto an entity without boxing its values where the runtime cannot generate code.
    /// </summary>
    /// <remarks>
    ///     What it writes for is found by looking for the calls that name an entity type - <c>Define&lt;T&gt;</c>
    ///     and <c>DefineDynamic( typeof( T ) )</c> - rather than by asking the caller to say so. A mapping the
    ///     search does not find is not broken by that: it builds its own accessors, exactly as it does today.
    /// </remarks>
    [Generator( LanguageNames.CSharp )]
    public sealed class MappingAccessorGenerator : IIncrementalGenerator
    {
        private const string MapperNamespace = "FlatFiles.TypeMapping";

        private static readonly string[] MapperTypes = ["DelimitedTypeMapper", "FixedLengthTypeMapper"];

        /// <summary>
        ///     Said about a member nothing can be written for. Information rather than a warning: a mapping that
        ///     falls back is not wrong, only slower, and a build that shouted about every one would be a build
        ///     nobody reads. It is said at all because a member quietly reverting to the slower path is how an
        ///     expensive mistake hides, which has happened in this library before.
        /// </summary>
        private static readonly DiagnosticDescriptor MemberNotWritten = new(
            "FF1001",
            "A member has no generated accessor",
            "'{0}.{1}' has no generated setter, because {2}; a mapping that uses it builds one at run time instead",
            "FlatFiles.Mapping",
            DiagnosticSeverity.Info,
            true );

        /// <inheritdoc />
        public void Initialize( IncrementalGeneratorInitializationContext context )
        {
            var entities = context.SyntaxProvider
                .CreateSyntaxProvider( static ( node, _ ) => IsWorthLookingAt( node ), static ( syntax, _ ) => Entity( syntax ) )
                .Where( static entity => entity.HasValue )
                .Select( static ( entity, _ ) => entity!.Value )
                .Collect();

            context.RegisterSourceOutput( entities, static ( output, found ) => Emit( output, found ) );
        }

        /// <summary>
        ///     A cheap look at the shape of a node, run for every node in the compilation, so it does as little as
        ///     it can: the name has to match before anything asks what the name means.
        /// </summary>
        private static bool IsWorthLookingAt( SyntaxNode node )
        {
            if (node is not InvocationExpressionSyntax invocation || invocation.Expression is not MemberAccessExpressionSyntax access)
            {
                return false;
            }
            var name = access.Name switch
            {
                GenericNameSyntax generic => generic.Identifier.ValueText,
                SimpleNameSyntax simple => simple.Identifier.ValueText,
                _ => null
            };
            return name is "Define" or "DefineDynamic";
        }

        /// <summary>
        ///     What the call actually names, once the compilation can be asked. Answers nothing where the call is
        ///     not one of this library's, or where the type it names is not one anything could be written for.
        /// </summary>
        private static MappedEntity? Entity( GeneratorSyntaxContext syntax )
        {
            var invocation = (InvocationExpressionSyntax) syntax.Node;
            if (syntax.SemanticModel.GetSymbolInfo( invocation ).Symbol is not IMethodSymbol method)
            {
                return null;
            }
            var declaring = method.ContainingType;
            if (declaring is null || declaring.ContainingNamespace?.ToDisplayString() != MapperNamespace)
            {
                return null;
            }
            if (!MapperTypes.Contains( declaring.Name ))
            {
                return null;
            }

            var entity = method.Name switch
            {
                "Define" when method.TypeArguments.Length == 1 => method.TypeArguments[0],
                "DefineDynamic" => NamedByTypeOf( invocation, syntax.SemanticModel ),
                _ => null
            };
            return entity is INamedTypeSymbol named ? Describe( named ) : null;
        }

        /// <summary>
        ///     The type in <c>DefineDynamic( typeof( T ) )</c>. A call handed a <see cref="System.Type" /> worked
        ///     out at run time names nothing that can be written for, and says so by answering nothing.
        /// </summary>
        private static ITypeSymbol? NamedByTypeOf( InvocationExpressionSyntax invocation, SemanticModel model )
        {
            var first = invocation.ArgumentList.Arguments.FirstOrDefault();
            if (first?.Expression is not TypeOfExpressionSyntax typeOf)
            {
                return null;
            }
            return model.GetTypeInfo( typeOf.Type ).Type;
        }

        private static MappedEntity? Describe( INamedTypeSymbol entity )
        {
            // A type parameter, an anonymous type or one the generated code could not name is not something to
            // write registrations for.
            if (entity.TypeKind != TypeKind.Class && entity.TypeKind != TypeKind.Struct)
            {
                return null;
            }
            if (entity.IsAbstract || entity.IsAnonymousType || entity.IsUnboundGenericType || entity.IsStatic)
            {
                return null;
            }
            if (entity.DeclaredAccessibility == Accessibility.Private || entity.DeclaredAccessibility == Accessibility.Protected)
            {
                return null;
            }

            var qualified = entity.ToDisplayString( SymbolDisplayFormat.FullyQualifiedFormat );
            // The alias belongs in the code, where it keeps the name unambiguous, and not in what the file and
            // the class are called - global__Customer names nothing a reader recognises.
            var readable = qualified.StartsWith( "global::" ) ? qualified.Substring( "global::".Length ) : qualified;
            var hint = new StringBuilder( readable.Length );
            foreach (var character in readable)
            {
                hint.Append( char.IsLetterOrDigit( character ) ? character : '_' );
            }
            return new MappedEntity( qualified, hint.ToString(), HasParameterlessConstructor( entity ), Members( entity ) );
        }

        private static bool HasParameterlessConstructor( INamedTypeSymbol entity )
        {
            foreach (var constructor in entity.InstanceConstructors)
            {
                if (constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility != Accessibility.Private)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        ///     Every property of the entity, and for each either what to write or why nothing can be.
        /// </summary>
        private static ImmutableArray<MappedMember> Members( INamedTypeSymbol entity )
        {
            var members = ImmutableArray.CreateBuilder<MappedMember>();
            var seen = new HashSet<string>();
            for (var type = entity; type is not null; type = type.BaseType)
            {
                foreach (var symbol in type.GetMembers())
                {
                    if (symbol is not IPropertySymbol property || property.IsStatic || property.IsIndexer)
                    {
                        continue;
                    }
                    if (property.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }
                    // A property hiding one of the same name further up wins, a mapping having named the one the
                    // entity itself declares.
                    if (!seen.Add( property.Name ))
                    {
                        continue;
                    }
                    members.Add( Member( property ) );
                }
            }
            return members.ToImmutable();
        }

        private static MappedMember Member( IPropertySymbol property )
        {
            var where = Position( property );
            if (property.SetMethod is not { } setter)
            {
                return MappedMember.Refused( property.Name, "it has no setter", where );
            }
            if (setter.DeclaredAccessibility != Accessibility.Public)
            {
                return MappedMember.Refused( property.Name, "its setter is not public", where );
            }
            if (setter.IsInitOnly)
            {
                return MappedMember.Refused( property.Name, "its setter is init-only, and can only be used while the entity is being built", where );
            }

            var underlying = Underlying( property.Type );
            var parsed = underlying ?? property.Type;
            if (!CanBeParsedTo( parsed ))
            {
                return MappedMember.Refused( property.Name, $"no column parses to {parsed.ToDisplayString()}", where );
            }
            return MappedMember.Writable( property.Name, parsed.ToDisplayString( SymbolDisplayFormat.FullyQualifiedFormat ), underlying is not null );
        }

        /// <summary>
        ///     The type inside a <c>Nullable&lt;T&gt;</c>, or null where the type is not one.
        /// </summary>
        private static ITypeSymbol? Underlying( ITypeSymbol type )
        {
            if (type is INamedTypeSymbol named && named.IsGenericType && named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
            {
                return named.TypeArguments[0];
            }
            return null;
        }

        /// <summary>
        ///     Whether the library has a column that reads a value of this type. A member of any other type is
        ///     left alone, since a registration nothing matches would be written and never used.
        /// </summary>
        private static bool CanBeParsedTo( ITypeSymbol type )
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                return true;
            }
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Char:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_String:
                case SpecialType.System_DateTime:
                    return true;
            }
            if (type is IArrayTypeSymbol array)
            {
                return array.Rank == 1
                    && ( array.ElementType.SpecialType == SpecialType.System_Byte || array.ElementType.SpecialType == SpecialType.System_Char );
            }
            return type.ToDisplayString() is "System.Guid" or "System.TimeSpan" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly";
        }

        private static SourcePosition Position( ISymbol symbol )
        {
            var location = symbol.Locations.FirstOrDefault( static x => x.IsInSource );
            if (location is null)
            {
                return default;
            }
            var span = location.GetLineSpan();
            return new SourcePosition( location.SourceTree?.FilePath ?? string.Empty,
                location.SourceSpan.Start, location.SourceSpan.Length,
                span.StartLinePosition.Line, span.StartLinePosition.Character,
                span.EndLinePosition.Line, span.EndLinePosition.Character );
        }

        private static void Emit( SourceProductionContext output, ImmutableArray<MappedEntity> found )
        {
            var written = new HashSet<string>();
            foreach (var entity in found)
            {
                if (!written.Add( entity.QualifiedName ))
                {
                    continue;
                }
                foreach (var member in entity.Members)
                {
                    if (member.Refusal is not null)
                    {
                        output.ReportDiagnostic( Diagnostic.Create( MemberNotWritten, Where( member.Position ), entity.QualifiedName, member.Name, member.Refusal ) );
                    }
                }
                var writable = entity.Members.Where( static x => x.Refusal is null ).ToImmutableArray();
                if (!entity.CanBeConstructed && writable.Length == 0)
                {
                    continue;
                }
                output.AddSource( $"{entity.HintName}.Accessors.g.cs", SourceText.From( Registrations( entity, writable ), Encoding.UTF8 ) );
            }
        }

        private static Location? Where( SourcePosition position )
        {
            if (string.IsNullOrEmpty( position.FilePath ))
            {
                return null;
            }
            return Location.Create( position.FilePath!,
                new TextSpan( position.Start, position.Length ),
                new LinePositionSpan(
                    new LinePosition( position.StartLine, position.StartCharacter ),
                    new LinePosition( position.EndLine, position.EndCharacter ) ) );
        }

        private static string Registrations( MappedEntity entity, ImmutableArray<MappedMember> writable )
        {
            var builder = new StringBuilder();
            builder.AppendLine( "// <auto-generated/>" );
            builder.AppendLine( "#nullable enable" );
            builder.AppendLine();
            builder.AppendLine( "namespace FlatFiles.Generated" );
            builder.AppendLine( "{" );
            builder.AppendLine( $"    /// <summary>What a mapping of <see cref=\"{entity.QualifiedName}\" /> would otherwise work out at run time.</summary>" );
            builder.AppendLine( $"    internal static class {entity.HintName}Accessors" );
            builder.AppendLine( "    {" );
            builder.AppendLine( "        [global::System.Runtime.CompilerServices.ModuleInitializer]" );
            builder.AppendLine( "        internal static void Register()" );
            builder.AppendLine( "        {" );
            if (entity.CanBeConstructed)
            {
                builder.AppendLine( $"            global::FlatFiles.CodeGeneration.MappingAccessors.AddFactory( static () => new {entity.QualifiedName}() );" );
            }
            foreach (var member in writable)
            {
                var method = member.IsNullable ? "AddNullableSetter" : "AddSetter";
                builder.AppendLine( $"            global::FlatFiles.CodeGeneration.MappingAccessors.{method}<{entity.QualifiedName}, {member.ValueType}>( \"{member.Name}\", static ( entity, value ) => entity.{member.Name} = value );" );
            }
            builder.AppendLine( "        }" );
            builder.AppendLine( "    }" );
            builder.AppendLine( "}" );
            return builder.ToString();
        }
    }
}
