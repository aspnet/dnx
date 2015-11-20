using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.CompilationAbstractions.Caching;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class SigningFacts
    {
        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.CLR)]
        public void UseOssSigningEqualsFalseReturnsWarningOnCoreClrAndMono()
        {
            RoslynCompiler compiler;
            CompilationProjectContext projectContext;
            PrepareCompilation(new FakeCompilerOptions { KeyFile = "keyfile.snk", UseOssSigning = false }, out compiler, out projectContext);

            var compilationContext = compiler.CompileProject(projectContext, Enumerable.Empty<IMetadataReference>(),
                Enumerable.Empty<ISourceReference>(), () => new List<ResourceDescriptor>(), "Debug");

            Assert.Equal(1, compilationContext.Diagnostics.Count);
            Assert.Equal("DNX1001", compilationContext.Diagnostics[0].Id);
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono | RuntimeFrameworks.CoreCLR)]
        public void UseOssSigningEqualsFalseReturnsNoWarningOnDesktopClr()
        {
            RoslynCompiler compiler;
            CompilationProjectContext projectContext;
            PrepareCompilation(new FakeCompilerOptions { KeyFile = "keyfile.snk", UseOssSigning = false }, out compiler, out projectContext);

            var compilationContext = compiler.CompileProject(projectContext, Enumerable.Empty<IMetadataReference>(),
                Enumerable.Empty<ISourceReference>(), () => new List<ResourceDescriptor>(), "Debug");

            Assert.Equal(0, compilationContext.Diagnostics.Count);
        }

        private static void PrepareCompilation(ICompilerOptions compilerOptions, out RoslynCompiler compiler,
            out CompilationProjectContext projectContext)
        {
            var cacheContextAccessor = new FakeCacheContextAccessor { Current = new CacheContext(null, (d) => { }) };
            compiler = new RoslynCompiler(null, cacheContextAccessor, new FakeNamedDependencyProvider(), null, null, null);
            var compilationTarget = new CompilationTarget("test", new FrameworkName(".NET Framework, Version=4.0"), "Release", null);
            projectContext = new CompilationProjectContext(
                compilationTarget, Directory.GetCurrentDirectory(), "project.json", "title", "description", "copyright",
                "1.0.0", new System.Version(1, 0), false, new CompilationFiles(Enumerable.Empty<string>(),
                Enumerable.Empty<string>()), compilerOptions);
        }
    }
}
