using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynProjectReferenceProvider : IProjectReferenceProvider
    {
        private readonly RoslynCompiler _compiler;

        public RoslynProjectReferenceProvider(ICache cache, IFileWatcher watcher)
        {
            _compiler = new RoslynCompiler(cache, watcher);
        }

        public IMetadataProjectReference GetProjectReference(
            Project project,
            FrameworkName targetFramework,
            string configuration,
            Func<ILibraryExport> referenceResolver,
            IList<IMetadataReference> outgoingReferences)
        {
            var export = referenceResolver();
            var incomingReferences = export.MetadataReferences;
            var incomingSourceReferences = export.SourceReferences;

            var compliationContext = _compiler.CompileProject(
                project,
                targetFramework,
                configuration,
                incomingReferences,
                incomingSourceReferences,
                outgoingReferences);

            if (compliationContext == null)
            {
                return null;
            }

            // Project reference
            return new RoslynProjectReference(compliationContext);
        }
    }
}