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
            // A type parameter, an array, an anonymous type or one the generated code could not name is not
            // something to write registrations for.
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
            return new MappedEntity( qualified, hint.ToString(), HasParameterlessConstructor( entity ) );
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

        private static void Emit( SourceProductionContext output, ImmutableArray<MappedEntity> found )
        {
            var written = new HashSet<string>();
            foreach (var entity in found)
            {
                if (!entity.CanBeConstructed || !written.Add( entity.QualifiedName ))
                {
                    continue;
                }
                output.AddSource( $"{entity.HintName}.Accessors.g.cs", SourceText.From( Registration( entity ), Encoding.UTF8 ) );
            }
        }

        private static string Registration( MappedEntity entity )
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
            builder.AppendLine( $"            global::FlatFiles.CodeGeneration.MappingAccessors.AddFactory( static () => new {entity.QualifiedName}() );" );
            builder.AppendLine( "        }" );
            builder.AppendLine( "    }" );
            builder.AppendLine( "}" );
            return builder.ToString();
        }
    }
}
