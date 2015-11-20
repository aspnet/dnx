using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.CompilationAbstractions.Caching;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class RoslynCompilerTest
    {
        private const string TestFrameworkName = "DNX,Version=v4.5.1";
        private const string TestName = "Test name";
        private const string TestTitle = "Test title";
        private const string TestDescription = "Test description";
        private const string TestCopyright = "Test copyright";
        private const string TestAssemblyFileVersion = "1.2.3.4";
        private const string TestVersion = "1.2.3-rc1";

        [Fact]
        public void FlowsProjectPropertiesIntoAssembly()
        {
            // Arrange
            // Act
            var compilationContext = Compile(new FakeCompilerOptions(),
                new CompilationTarget(TestName, new FrameworkName(TestFrameworkName),
                string.Empty,
                string.Empty));

            // Assert
            var expectedAttributes = new Dictionary<string, string>
            {
                [typeof(AssemblyTitleAttribute).FullName] = TestTitle,
                [typeof(AssemblyDescriptionAttribute).FullName] = TestDescription,
                [typeof(AssemblyCopyrightAttribute).FullName] = TestCopyright,
                [typeof(AssemblyFileVersionAttribute).FullName] = TestAssemblyFileVersion,
                [typeof(AssemblyVersionAttribute).FullName] = TestVersion.Substring(0, TestVersion.IndexOf('-')),
                [typeof(AssemblyInformationalVersionAttribute).FullName] = TestVersion,
            };
            var compilationAttributes = compilationContext.Compilation.Assembly.GetAttributes();

            Assert.All(compilationAttributes, compilationAttribute => expectedAttributes[compilationAttribute.AttributeClass.ToString()].Equals(
                compilationAttribute.ConstructorArguments.First().Value));
        }

        [Fact]
        public void DoesNotSignAndEmitEntryPoinInPreproccessAssembly()
        {
            // Arrange
            var compilerOptions = new FakeCompilerOptions()
            {
                KeyFile = "keyfile.snk",
                DelaySign = true,
                EmitEntryPoint = true
            };

            var target = new CompilationTarget(TestName, new FrameworkName(TestFrameworkName), string.Empty, "preprocess");

            // Act
            var compilationContext = Compile(compilerOptions, target);

            Assert.Equal(OutputKind.DynamicallyLinkedLibrary, compilationContext.Compilation.Options.OutputKind);
            Assert.Equal(null, compilationContext.Compilation.Options.CryptoKeyFile);
            Assert.Equal(0, compilationContext.Compilation.Options.CryptoPublicKey.Length);
            Assert.False(compilationContext.Compilation.Options.DelaySign);
        }

        [Fact]
        public void DoesNotOssSignAndInPreproccessAssembly()
        {
            // Arrange
            var compilerOptions = new FakeCompilerOptions()
            {
                UseOssSigning = true
            };

            var target = new CompilationTarget(TestName, new FrameworkName(TestFrameworkName), string.Empty, "preprocess");

            // Act
            var compilationContext = Compile(compilerOptions, target);

            Assert.Equal(OutputKind.DynamicallyLinkedLibrary, compilationContext.Compilation.Options.OutputKind);
            Assert.Equal(null, compilationContext.Compilation.Options.CryptoKeyFile);
            Assert.Equal(0, compilationContext.Compilation.Options.CryptoPublicKey.Length);
            Assert.False(compilationContext.Compilation.Options.DelaySign);
        }

        private static CompilationContext Compile(FakeCompilerOptions compilerOptions, CompilationTarget target)
        {
            var cacheContextAccessor = new FakeCacheContextAccessor {Current = new CacheContext(null, (d) => { })};

            var compilationProjectContext = new CompilationProjectContext(
               target,
                Directory.GetCurrentDirectory(),
                "project.json",
                TestTitle,
                TestDescription,
                TestCopyright,
                TestVersion,
                Version.Parse(TestAssemblyFileVersion),
                false,
                new CompilationFiles(
                    new List<string> {},
                    new List<string> {}),
                compilerOptions);

            var compiler = new RoslynCompiler(null, cacheContextAccessor, new FakeNamedDependencyProvider(), null, null, null);

            var assembly = typeof (object).GetTypeInfo().Assembly;
            var metadataReference = new FakeMetadataReference()
            {
                MetadataReference = MetadataReference.CreateFromFile((string)assembly.GetType().GetProperty("Location").GetValue(assembly))
            };

            var compilationContext = compiler.CompileProject(
                compilationProjectContext,
                new List<IMetadataReference> { metadataReference },
                new List<ISourceReference> {},
                () => new List<ResourceDescriptor>(),
                "Debug");
            return compilationContext;
        }
    }
}
