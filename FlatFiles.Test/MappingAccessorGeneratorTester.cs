using System;
using System.Collections.Generic;
using System.Linq;
using FlatFiles.Generator;
using FlatFiles.TypeMapping;
using Microsoft.CodeAnalysis;
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
        public void TestTypeWithoutAParameterlessConstructor_IsNotWrittenFor()
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

            Assert.IsEmpty( written, "There is no factory to register for a type that cannot be built empty." );
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
            var compilation = CSharpCompilation.Create( "Consumer",
                [CSharpSyntaxTree.ParseText( source )],
                References(),
                new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary ) );

            var errors = compilation.GetDiagnostics().Where( x => x.Severity == DiagnosticSeverity.Error ).ToList();
            Assert.IsEmpty( errors, $"The source under test does not compile: {string.Join( "; ", errors.Select( x => x.GetMessage() ) )}" );

            var driver = CSharpGeneratorDriver.Create( new MappingAccessorGenerator() );
            var result = driver.RunGenerators( compilation ).GetRunResult();

            Assert.IsEmpty( result.Diagnostics, "The generator reported something it should not have." );
            return [.. result.GeneratedTrees.Select( x => x.GetText().ToString() )];
        }

        private static List<MetadataReference> References()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where( x => !x.IsDynamic && !string.IsNullOrEmpty( x.Location ) );
            List<MetadataReference> references = [.. assemblies.Select( x => (MetadataReference) MetadataReference.CreateFromFile( x.Location ) )];
            references.Add( MetadataReference.CreateFromFile( typeof( IDelimitedTypeMapper<> ).Assembly.Location ) );
            return references;
        }
    }
}
