using Microsoft.CodeAnalysis;

namespace FlatFiles.Generator
{
    /// <summary>
    ///     Writes the accessor registrations a mapping would otherwise have to build at run time, so that a type
    ///     mapper reads onto an entity without boxing its values where the runtime cannot generate code.
    /// </summary>
    /// <remarks>
    ///     Nothing yet. The project exists first so that what shipping a generator does to the package can be
    ///     answered before there is a generator worth keeping: an analyser changes the package's layout without
    ///     changing its public surface, and finding out afterwards that the two disagree would be an expensive
    ///     way to learn it.
    /// </remarks>
    [Generator( LanguageNames.CSharp )]
    public sealed class MappingAccessorGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize( IncrementalGeneratorInitializationContext context )
        {
            // Deliberately empty. A generator that registers no step runs no step, which is what makes this a
            // measurement of the packaging rather than of anything it might emit.
        }
    }
}
