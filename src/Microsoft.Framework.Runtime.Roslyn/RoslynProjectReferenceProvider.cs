using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynProjectReferenceProvider : IProjectReferenceProvider
    {
        private readonly RoslynCompiler _compiler;

        public RoslynProjectReferenceProvider(
            ICache cache,
            ICacheContextAccessor cacheContextAccessor,
            INamedCacheDependencyProvider namedCacheProvider,
            IAssemblyLoadContextFactory loadContextFactory,
            IFileWatcher watcher,
            IApplicationEnvironment environment,
            IServiceProvider services)
        {
            _compiler = new RoslynCompiler(
                cache,
                cacheContextAccessor,
                namedCacheProvider,
                loadContextFactory,
                watcher,
                environment,
                services);
        }

        public IMetadataProjectReference GetProjectReference(
            Project project,
            ILibraryKey target,
            Func<ILibraryExport> referenceResolver,
            IList<IMetadataReference> outgoingReferences)
        {
            var export = referenceResolver();
            var incomingReferences = export.MetadataReferences;
            var incomingSourceReferences = export.SourceReferences;

            var compilationContext = _compiler.CompileProject(
                project,
                target,
                incomingReferences,
                incomingSourceReferences,
                outgoingReferences);

            if (compilationContext == null)
            {
                return null;
            }

            // Project reference
            return new RoslynProjectReference(compilationContext);
        }
    }
}
