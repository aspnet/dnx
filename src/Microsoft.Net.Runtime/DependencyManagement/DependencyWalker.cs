using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class DependencyWalker
    {
        private readonly IEnumerable<IDependencyProvider> _dependencyProviders;

        public DependencyWalker(IEnumerable<IDependencyProvider> dependencyProviders)
        {
            _dependencyProviders = dependencyProviders;
        }

        public void Walk(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            var sw = Stopwatch.StartNew();
            Trace.TraceInformation("Walking dependency graph for '{0} {1}'.", name, targetFramework);

            var context = new WalkContext();

            context.Walk(
                _dependencyProviders,
                name,
                version,
                targetFramework);

            context.Populate(targetFramework);

            sw.Stop();
            Trace.TraceInformation("Resolved dependencies for {0} in {1}ms", name, sw.ElapsedMilliseconds);
        }
    }
}
