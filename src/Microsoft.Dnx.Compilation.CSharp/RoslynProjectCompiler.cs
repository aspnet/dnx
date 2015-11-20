using System;
using System.Collections.Generic;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.CompilationAbstractions.Caching;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class RoslynProjectCompiler : IProjectCompiler
    {
        private readonly RoslynCompiler _compiler;

        public RoslynProjectCompiler(
            ICache cache,
            ICacheContextAccessor cacheContextAccessor,
            INamedCacheDependencyProvider namedCacheProvider,
            IAssemblyLoadContext loadContext,
            IApplicationEnvironment environment,
            IServiceProvider services)
        {
            _compiler = new RoslynCompiler(
                cache,
                cacheContextAccessor,
                namedCacheProvider,
                loadContext,
                environment,
                services);
        }

        public IMetadataProjectReference CompileProject(
            CompilationProjectContext projectContext,
            Func<LibraryExport> referenceResolver,
            Func<IList<ResourceDescriptor>> resourcesResolver,
            string configuration)
        {
            var export = referenceResolver();
            if (export == null)
            {
                return null;
            }

            var incomingReferences = export.MetadataReferences;
            var incomingSourceReferences = export.SourceReferences;

            var compliationContext = _compiler.CompileProject(
                projectContext,
                incomingReferences,
                incomingSourceReferences,
                resourcesResolver,
                configuration);

            if (compliationContext == null)
            {
                return null;
            }

            // Project reference
            return new RoslynProjectReference(compliationContext);
        }
    }
}
