using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class SigningFacts
    {
        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono | RuntimeFrameworks.CLR)]
        public void CompileIgnoresRealStrongNameSigningOnCoreClr()
        {
            RoslynCompiler compiler;
            CompilationProjectContext projectContext;
            PrepareCompilation(new FakeCompilerOptions { KeyFile = "keyFile.snk" }, out compiler, out projectContext);

            var compilationContext = compiler.CompileProject(projectContext, Enumerable.Empty<IMetadataReference>(),
                Enumerable.Empty<ISourceReference>(), () => new List<ResourceDescriptor>());

            Assert.Equal(1, compilationContext.Diagnostics.Count);
            Assert.Equal("DNX1001", compilationContext.Diagnostics[0].Id);
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.CLR | RuntimeFrameworks.CoreCLR)]
        public void CompileFallsBackToOssSigningIfKeyFileSpecifiedOnMono()
        {
            RoslynCompiler compiler;
            CompilationProjectContext projectContext;
            PrepareCompilation(new FakeCompilerOptions { KeyFile = "keyFile.snk" }, out compiler, out projectContext);

            var compilationContext = compiler.CompileProject(projectContext, Enumerable.Empty<IMetadataReference>(),
                Enumerable.Empty<ISourceReference>(), () => new List<ResourceDescriptor>());

            Assert.Equal(1, compilationContext.Diagnostics.Count);
            Assert.Equal("DNX1002", compilationContext.Diagnostics[0].Id);
        }

        [Fact]
        public void OssSigningAndRealSigningAreMutuallyExclusive()
        {
            RoslynCompiler compiler;
            CompilationProjectContext projectContext;
            PrepareCompilation(new FakeCompilerOptions { KeyFile = "keyFile.snk", StrongName = true }, out compiler, out projectContext);

            var compilationContext = compiler.CompileProject(projectContext, Enumerable.Empty<IMetadataReference>(),
                Enumerable.Empty<ISourceReference>(), () => new List<ResourceDescriptor>());

            Assert.Equal(1, compilationContext.Diagnostics.Count);
            Assert.Equal("DNX1003", compilationContext.Diagnostics[0].Id);
        }

        private static void PrepareCompilation(ICompilerOptions compilerOptions, out RoslynCompiler compiler,
            out CompilationProjectContext projectContext)
        {
            var cacheContextAccessor = new FakeCacheContextAccessor { Current = new CacheContext(null, (d) => { }) };
            compiler = new RoslynCompiler(null, cacheContextAccessor, new FakeNamedDependencyProvider(), null, new FakeWatcher(), null, null);
            var compilationTarget = new CompilationTarget("test", new FrameworkName(".NET Framework, Version=4.0"), "Release", null);
            projectContext = new CompilationProjectContext(
                compilationTarget, Directory.GetCurrentDirectory(), "project.json", "1.0.0", new System.Version(1, 0), false,
                new CompilationFiles(Enumerable.Empty<string>(), Enumerable.Empty<string>()), compilerOptions);
        }

        private class FakeWatcher : IFileWatcher
        {
            public event Action<string> OnChanged;

            public void Dispose() { }

            public void WatchDirectory(string path, string extension) { }

            public bool WatchFile(string path)
            {
                return false;
            }

            public void WatchProject(string path) { }
        }

        private class FakeCacheContextAccessor : ICacheContextAccessor
        {
            public CacheContext Current { get; set; }
        }

        private class FakeCompilerOptions : ICompilerOptions
        {
            public bool? AllowUnsafe { get; set; }

            public IEnumerable<string> Defines { get; set; }

            public bool? DelaySign { get; set; }

            public bool? EmitEntryPoint { get; set; }

            public string KeyFile { get; set; }

            public string LanguageVersion { get; set; }

            public bool? Optimize { get; set; }

            public string Platform { get; set; }

            public bool? StrongName { get; set; }

            public bool? WarningsAsErrors { get; set; }
        }

        private class FakeNamedDependencyProvider : INamedCacheDependencyProvider
        {
            public ICacheDependency GetNamedDependency(string name)
            {
                return null;
            }

            public void Trigger(string name)
            { }
        }
    }
}
