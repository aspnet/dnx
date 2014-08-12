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
            FrameworkName targetFramework,
            string configuration,
            Func<ILibraryExport> referenceResolver,
            IList<IMetadataReference> outgoingReferences)
        {
            var task = _compiler.Compile(new CompileRequest
            {
                ProjectPath = project.ProjectDirectory,
                Configuration = configuration,
                TargetFramework = targetFramework.ToString()
            });

            foreach (var embeddedReference in task.Result.EmbeddedReferences)
            {
                outgoingReferences.Add(new EmbeddedMetadataReference(embeddedReference.Key, embeddedReference.Value));
            }

            return new DesignTimeProjectReference(project, task.Result);
        }
    }
}