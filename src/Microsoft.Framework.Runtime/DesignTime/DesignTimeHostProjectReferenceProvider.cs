using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class DesignTimeHostProjectReferenceProvider : IProjectReferenceProvider
    {
        private readonly IDesignTimeHostCompiler _compiler;

        public DesignTimeHostProjectReferenceProvider(IDesignTimeHostCompiler compiler)
        {
            _compiler = compiler;
        }

        public IMetadataProjectReference GetProjectReference(
            Project project,
            ILibraryKey target,
            Func<ILibraryExport> referenceResolver,
            IList<IMetadataReference> outgoingReferences)
        {
            // The target framework and configuration are assumed to be correct
            // in the design time process
            var task = _compiler.Compile(project.ProjectDirectory, target);

            foreach (var embeddedReference in task.Result.EmbeddedReferences)
            {
                outgoingReferences.Add(new EmbeddedMetadataReference(embeddedReference.Key, embeddedReference.Value));
            }

            return new DesignTimeProjectReference(project, task.Result);
        }
    }
}