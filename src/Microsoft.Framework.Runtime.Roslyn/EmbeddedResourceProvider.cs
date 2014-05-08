using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class EmbeddedResourceProvider : IResourceProvider
    {
        public IList<ResourceDescription> GetResources(Project project)
        {
            return project.ResourceFiles.Select(resourceFile => new ResourceDescription(
                Path.GetFileName(resourceFile),
                () => new FileStream(resourceFile, FileMode.Open, FileAccess.Read, FileShare.Read),
                true)).ToList();

        }
    }
}
