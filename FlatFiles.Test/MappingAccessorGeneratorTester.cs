using System;
using System.Collections.Generic;
using System.Linq;
using FlatFiles.Generator;
using FlatFiles.TypeMapping;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests what the generator finds to write for. It reads the calls that name an entity type rather than
    ///     asking the caller to declare one, so what these check is which calls it recognises, which types it
    ///     refuses, and that it says nothing at all about a compilation that does not map anything.
    /// </summary>
    [TestClass]
    public class MappingAccessorGeneratorTester
    {
        [TestMethod]
        public void TestDelimitedDefine_IsFound()
        {
            var written = Run( """
                using FlatFiles.TypeMapping;
                public class Customer { public int Id { get; set; } }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.Define<Customer>();
                }
                """ );

            Assert.ContainsSingle( written );
            StringAssert.Contains( written.Single(), "new global::Customer()" );
            StringAssert.Contains( written.Single(), "AddSetter<global::Customer, int>( \"Id\"" );
        }

        [TestMethod]
        public void TestDefineFromAttributes_IsFound()
        {
            // A new entry point the generator does not know is an entity that silently loses its accessors and
            // boxes under AOT, which is the one way this feature could undo three releases of work.
            var written = Run( """
                using FlatFiles;
                using FlatFiles.TypeMapping;
                public class Customer { [Column( Order = 0 )] public int Id { get; set; } }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.DefineFromAttributes<Customer>();
                }
                """ );

            Assert.ContainsSingle( written );
            StringAssert.Contains( written.Single(), "AddSetter<global::Customer, int>( \"Id\"" );
        }

        [TestMethod]
        public void TestFixedLengthDefine_IsFound()
        {
            var written = Run( """
                using FlatFiles.TypeMapping;
                public class Customer { public int Id { get; set; } }
                public static class Program
                {
                    public static void Main() => FixedLengthTypeMapper.Define<Customer>();
                }
                """ );

            Assert.ContainsSingle( written );
        }

        [TestMethod]
        public void TestDefineDynamic_NamingATypeDirectly_IsFound()
        {
            var written = Run( """
                using FlatFiles.TypeMapping;
                public class Customer { public int Id { get; set; } }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.DefineDynamic( typeof( Customer ) );
                }
                """ );

            Assert.ContainsSingle( written );
            StringAssert.Contains( written.Single(), "new global::Customer()" );
        }

        [TestMethod]
        public void TestDefineDynamic_NamingATypeAtRunTime_IsNotFound()
        {
            var written = Run( """
                using System;
                using FlatFiles.TypeMapping;
                public static class Program
                {
                    public static void Main( string[] args ) => DelimitedTypeMapper.DefineDynamic( Type.GetType( args[0] ) );
                }
                """ );

            Assert.IsEmpty( written, "Nothing can be written for a type the compilation does not know." );
        }

        [TestMethod]
        public void TestDefineOnSomethingElse_IsIgnored()
        {
            var written = Run( """
                public static class Mine
                {
                    public static void Define<T>() { }
                }
                public class Customer { public int Id { get; set; } }
                public static class Program
                {
                    public static void Main() => Mine.Define<Customer>();
                }
                """ );

            Assert.IsEmpty( written, "A method of the same name somewhere else is not this library's." );
        }

        [TestMethod]
        public void TestTypeWithoutAParameterlessConstructor_HasNoFactory()
        {
            var written = Run( """
                using FlatFiles.TypeMapping;
                public class Customer
                {
                    public Customer( int id ) => Id = id;
                    public int Id { get; }
                    public string Name { get; set; } = string.Empty;
                }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.Define<Customer>();
                }
                """ );

            Assert.DoesNotContain( "AddFactory", written.Single(), "There is nothing to register for a type that cannot be built empty." );
            StringAssert.Contains( written.Single(), "AddSetter<global::Customer, string>( \"Name\"" );
        }

        [TestMethod]
        public void TestTypeThatCanOnlyBeWrittenFrom_GetsItsGetters()
        {
            var written = Run( """
                using FlatFiles.TypeMapping;
                public class Customer
                {
                    public Customer( int id ) => Id = id;
                    public int Id { get; }
                }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.Define<Customer>();
                }
                """ );

            // Nothing can be read onto it, but everything about it can be written from - which is half a
            // mapping and worth having.
            StringAssert.Contains( written.Single(), "AddGetter<global::Customer, int>( \"Id\"" );
            Assert.DoesNotContain( "AddSetter", written.Single(), "There is no setter to register." );
            Assert.DoesNotContain( "AddFactory", written.Single(), "There is nothing to build it with." );
        }

        [TestMethod]
        public void TestTypeWithNothingReachable_IsNotWrittenForAtAll()
        {
            var written = Run( """
                using FlatFiles.TypeMapping;
                public class Customer
                {
                    public Customer( int id ) => Id = id;
                    private int Id { get; }
                }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.Define<Customer>();
                }
                """ );

            Assert.IsEmpty( written, "An empty registration is worse than none." );
        }

        [TestMethod]
        public void TestEveryKindOfMember_IsWrittenForOrExplained()
        {
            var written = Run( """
                using System;
                using FlatFiles.TypeMapping;
                public enum Rank { First }
                public class Customer
                {
                    public int Whole { get; set; }
                    public decimal? Amount { get; set; }
                    public DateTime When { get; set; }
                    public Guid Key { get; set; }
                    public Rank Standing { get; set; }
                    public byte[] Raw { get; set; } = [];
                    public string Text { get; set; } = string.Empty;
                    public int ReadOnly { get; }
                    public int Hidden { get; private set; }
                    public int Once { get; init; }
                    public Customer? Nested { get; set; }
                }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.Define<Customer>();
                }
                """ );

            var source = written.Single();
            StringAssert.Contains( source, "AddSetter<global::Customer, int>( \"Whole\"" );
            StringAssert.Contains( source, "AddNullableSetter<global::Customer, decimal>( \"Amount\"" );
            StringAssert.Contains( source, "AddSetter<global::Customer, global::System.DateTime>( \"When\"" );
            StringAssert.Contains( source, "AddSetter<global::Customer, global::System.Guid>( \"Key\"" );
            StringAssert.Contains( source, "AddSetter<global::Customer, global::Rank>( \"Standing\"" );
            StringAssert.Contains( source, "AddSetter<global::Customer, byte[]>( \"Raw\"" );
            StringAssert.Contains( source, "AddSetter<global::Customer, string>( \"Text\"" );

            // Readable but not settable, so each gets a getter and no setter.
            foreach (var readOnly in new[] { "ReadOnly", "Hidden", "Once" })
            {
                StringAssert.Contains( source, $"AddGetter<global::Customer, int>( \"{readOnly}\"" );
                Assert.DoesNotContain( $"AddSetter<global::Customer, int>( \"{readOnly}\"", source, $"{readOnly} cannot be set this way." );
            }
            Assert.DoesNotContain( $"\"Nested\"", source, "No column carries an entity." );

            // Everything writable is readable here, so each of those has both.
            StringAssert.Contains( source, $"AddGetter<global::Customer, int>( \"Whole\"" );
            StringAssert.Contains( source, $"AddNullableGetter<global::Customer, decimal>( \"Amount\"" );
        }

        [TestMethod]
        public void TestAMemberItCannotWriteFor_IsSaidAloudButNotShoutedAbout()
        {
            Run( """
                using FlatFiles.TypeMapping;
                public class Customer
                {
                    public int Id { get; set; }
                    public int Counted { get; private set; }
                }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.Define<Customer>();
                }
                """ );

            var said = Reported.Single();
            Assert.AreEqual( "FF1001", said.Id );
            Assert.AreEqual( DiagnosticSeverity.Info, said.Severity, "A mapping that falls back is slower, not wrong." );
            StringAssert.Contains( said.GetMessage(), "Counted" );
            StringAssert.Contains( said.GetMessage(), "its setter is not public" );
            Assert.AreNotEqual( Location.None, said.Location, "It should point at the member it is about." );
        }

        [TestMethod]
        public void TestAMemberOfATypeNoColumnReads_SaysSo()
        {
            Run( """
                using System;
                using FlatFiles.TypeMapping;
                public class Customer
                {
                    public int Id { get; set; }
                    public Uri? Where { get; set; }
                }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.Define<Customer>();
                }
                """ );

            var said = Reported.Single();
            StringAssert.Contains( said.GetMessage(), "no column reads or writes System.Uri" );
            Assert.AreEqual( DiagnosticSeverity.Info, said.Severity );
        }

        [TestMethod]
        public void TestTheSameTypeTwice_IsWrittenForOnce()
        {
            var written = Run( """
                using FlatFiles.TypeMapping;
                public class Customer { public int Id { get; set; } }
                public static class Program
                {
                    public static void Main()
                    {
                        DelimitedTypeMapper.Define<Customer>();
                        FixedLengthTypeMapper.Define<Customer>();
                    }
                }
                """ );

            Assert.ContainsSingle( written );
        }

        [TestMethod]
        public void TestAnEntityInANamespace_IsNamedInFull()
        {
            var written = Run( """
                using FlatFiles.TypeMapping;
                namespace Sales { public class Customer { public int Id { get; set; } } }
                public static class Program
                {
                    public static void Main() => DelimitedTypeMapper.Define<Sales.Customer>();
                }
                """ );

            StringAssert.Contains( written.Single(), "new global::Sales.Customer()" );
        }

        [TestMethod]
        public void TestNothingMapped_WritesNothing()
        {
            var written = Run( """
                public static class Program
                {
                    public static void Main() { }
                }
                """ );

            Assert.IsEmpty( written, "A compilation that maps nothing should be left alone." );
        }

        /// <summary>
        ///     Runs the generator over the source and answers what it wrote, having first insisted the source
        ///     itself compiles - a test that quietly stopped compiling would otherwise look like one that found
        ///     nothing to write.
        /// </summary>
        private static List<string> Run( string source )
        {
            return Run( source, null );
        }

        /// <summary>
        ///     The same, with the project having something to say about whether it wants anything written.
        /// </summary>
        /// <param name="source">The consumer's source.</param>
        /// <param name="generateAccessors">
        ///     What <c>FlatFilesGenerateAccessors</c> is set to, or null for a project that has not set it.
        /// </param>
        private static List<string> Run( string source, string? generateAccessors )
        {
            var compilation = CSharpCompilation.Create( "Consumer",
                [CSharpSyntaxTree.ParseText( source, path: "Consumer.cs" )],
                References(),
                new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary ) );

            var errors = compilation.GetDiagnostics().Where( x => x.Severity == DiagnosticSeverity.Error ).ToList();
            Assert.IsEmpty( errors, $"The source under test does not compile: {string.Join( "; ", errors.Select( x => x.GetMessage() ) )}" );

            var driver = CSharpGeneratorDriver.Create(
                [new MappingAccessorGenerator().AsSourceGenerator()],
                optionsProvider: new ProjectOptions( generateAccessors ) );
            var result = driver.RunGenerators( compilation ).GetRunResult();

            Reported = [.. result.Diagnostics];
            Assert.IsEmpty( Reported.Where( x => x.Severity >= DiagnosticSeverity.Warning ), "The generator warned about something it should not have." );
            return [.. result.GeneratedTrees.Select( x => x.GetText().ToString() )];
        }

        /// <summary>
        ///     What the last run said, for the tests that are about what it says rather than what it writes.
        /// </summary>
        private static List<Diagnostic> Reported { get; set; } = [];

        private static List<MetadataReference> References()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where( x => !x.IsDynamic && !string.IsNullOrEmpty( x.Location ) );
            List<MetadataReference> references = [.. assemblies.Select( x => (MetadataReference) MetadataReference.CreateFromFile( x.Location ) )];
            references.Add( MetadataReference.CreateFromFile( typeof( IDelimitedTypeMapper<> ).Assembly.Location ) );
            return references;
        }

        [TestMethod]
        public void TestGenerateAccessorsFalse_WritesNothing()
        {
            var written = Run( Mapping, "false" );

            Assert.IsEmpty( written, "A project that asked for nothing to be written should have nothing written for it." );
        }

        [TestMethod]
        public void TestGenerateAccessorsTrue_WritesAsUsual()
        {
            var written = Run( Mapping, "true" );

            Assert.IsNotEmpty( written, "Asking for what the package does by default should get it." );
            Assert.Contains( "AddSetter", string.Join( "", written ) );
        }

        [TestMethod]
        public void TestGenerateAccessorsUnset_WritesAsUsual()
        {
            var written = Run( Mapping, null );

            Assert.IsNotEmpty( written, "A project that has said nothing gets what the package is for." );
        }

        [TestMethod]
        public void TestGenerateAccessorsNotABoolean_WritesAsUsual()
        {
            // Better to carry on than to silently write nothing because somebody typed the value wrong.
            var written = Run( Mapping, "no thanks" );

            Assert.IsNotEmpty( written, "A value that is not a boolean should not be read as a refusal." );
        }

        /// <summary>
        ///     A mapping of one entity, which the generator writes both directions for.
        /// </summary>
        private const string Mapping = """
            using FlatFiles.TypeMapping;
            public class Customer { public int Id { get; set; } }
            public static class Program
            {
                public static void Main() => DelimitedTypeMapper.Define<Customer>();
            }
            """;

        /// <summary>
        ///     What the project told the compiler, for the tests that are about being told.
        /// </summary>
        private sealed class ProjectOptions( string? generateAccessors ) : AnalyzerConfigOptionsProvider
        {
            public override AnalyzerConfigOptions GlobalOptions { get; } = new Global( generateAccessors );

            public override AnalyzerConfigOptions GetOptions( SyntaxTree tree )
            {
                return GlobalOptions;
            }

            public override AnalyzerConfigOptions GetOptions( AdditionalText textFile )
            {
                return GlobalOptions;
            }

            private sealed class Global( string? generateAccessors ) : AnalyzerConfigOptions
            {
                public override bool TryGetValue( string key, [NotNullWhen( true )] out string? value )
                {
                    if (key == "build_property.FlatFilesGenerateAccessors" && generateAccessors is not null)
                    {
                        value = generateAccessors;
                        return true;
                    }
                    value = null;
                    return false;
                }
            }
        }
    }
}
