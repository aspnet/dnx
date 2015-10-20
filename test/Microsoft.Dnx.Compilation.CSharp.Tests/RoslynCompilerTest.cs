using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
#if DNX451
using Moq;
#endif
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class RoslynCompilerTest
    {
#if DNX451
        [Fact]
        public void FlowsProjectPropertiesIntoAssembly()
        {
            const string testName = "Test name";
            const string testTitle = "Test title";
            const string testDescription = "Test description";
            const string testCopyright = "Test copyright";
            const string testAssemblyFileVersion = "1.2.3.4";
            const string testVersion = "1.2.3-rc1";
            const string testFrameworkName = "DNX,Version=v4.5.1";

            // Arrange
            var compilationProjectContext = new CompilationProjectContext(
                new CompilationTarget(testName, new FrameworkName(testFrameworkName), string.Empty, string.Empty),
                string.Empty,
                string.Empty,
                testTitle,
                testDescription,
                testCopyright,
                testVersion,
                new Version(testAssemblyFileVersion),
                false,
                new CompilationFiles(
                    new List<string> { },
                    new List<string> { }),
                new Mock<ICompilerOptions>().Object);
            var compiler = new RoslynCompiler(
                new Mock<ICache>().Object,
                new Mock<ICacheContextAccessor>().Object,
                new Mock<INamedCacheDependencyProvider>().Object,
                new Mock<IAssemblyLoadContext>().Object,
                new Mock<IApplicationEnvironment>().Object,
                new Mock<IServiceProvider>().Object);
            var metadataReference = new Mock<IRoslynMetadataReference>();
            metadataReference
                .Setup(reference => reference.MetadataReference)
                .Returns(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            // Act
            var compilationContext = compiler.CompileProject(
                compilationProjectContext,
                new List<IMetadataReference> { metadataReference.Object },
                new List<ISourceReference> { },
                () => new List<ResourceDescriptor> { });

            // Assert
            var expectedAttributes = new Dictionary<string, string>
            {
                [typeof(AssemblyTitleAttribute).FullName] = testTitle,
                [typeof(AssemblyDescriptionAttribute).FullName] = testDescription,
                [typeof(AssemblyCopyrightAttribute).FullName] = testCopyright,
                [typeof(AssemblyFileVersionAttribute).FullName] = testAssemblyFileVersion,
                [typeof(AssemblyVersionAttribute).FullName] = testVersion.Substring(0, testVersion.IndexOf('-')),
                [typeof(AssemblyInformationalVersionAttribute).FullName] = testVersion,
            };
            var compilationAttributes = compilationContext.Compilation.Assembly.GetAttributes();

            Assert.All(compilationAttributes, compilationAttribute => expectedAttributes[compilationAttribute.AttributeClass.ToString()].Equals(
                compilationAttribute.ConstructorArguments.First().Value));
        }
#endif
    }
}
