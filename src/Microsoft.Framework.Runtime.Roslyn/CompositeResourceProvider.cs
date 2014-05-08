using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompositeResourceProvider : IResourceProvider
    {
        private readonly IEnumerable<IResourceProvider> _providers;

        public CompositeResourceProvider(IEnumerable<IResourceProvider> providers)
        {
            _providers = providers;
        }

        public IList<ResourceDescription> GetResources(Project project)
        {
            return _providers.SelectMany(provider => provider.GetResources(project)).ToList();
        }
    }
}
