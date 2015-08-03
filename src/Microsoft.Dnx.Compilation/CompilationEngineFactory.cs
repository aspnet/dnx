using System;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Compilation;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationEngineFactory: ICompilationEngineFactory
    {
        public CompilationEngineFactory(IFileWatcher fileWatcher, CompilationCache cache)
        {
            CompilationCache = cache;
            FileWatcher = fileWatcher;
        }

        public IFileWatcher FileWatcher { get; }
        public CompilationCache CompilationCache { get; }

        public CompilationEngine CreateEngine(CompilationEngineContext context)
        {
            return CreateEngineCore(context);
        }

        // Having two versions of this allows consumers of the concrete Factory type to avoid casting the output by calling the
        // above version, while people calling through the interface (i.e. the runtime) can get the interface
        ICompilationEngine ICompilationEngineFactory.CreateEngine(CompilationEngineContext context)
        {
            return CreateEngineCore(context);
        }

        private CompilationEngine CreateEngineCore(CompilationEngineContext context)
        {
            return new CompilationEngine(
                CompilationCache,
                FileWatcher,
                context);
        }
    }
}
