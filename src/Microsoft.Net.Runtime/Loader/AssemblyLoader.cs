using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime.Loader.Infrastructure;
using Microsoft.Net.Runtime.Services;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public class AssemblyLoader : IAssemblyLoader, IDependencyExportResolver, IDependencyRefresher
    {
        private List<IAssemblyLoader> _loaders = new List<IAssemblyLoader>();

        public void Add(IAssemblyLoader loader)
        {
            _loaders.Add(loader);
        }

        public Assembly LoadAssembly(LoadContext loadContext)
        {
            var result = Load(loadContext);

            if (result == null)
            {
                return null;
            }

            if (result.Errors != null)
            {
                throw new Exception(String.Join(Environment.NewLine, result.Errors));
            }

            return result.Assembly;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            var sw = new Stopwatch();
            sw.Start();
            Trace.TraceInformation("Loading {0} for '{1}'.", loadContext.AssemblyName, loadContext.TargetFramework);

            try
            {
                return LoadImpl(loadContext, sw);
            }
            finally
            {
                sw.Stop();
                Trace.TraceInformation("Loaded {0} in {1}ms", loadContext.AssemblyName, sw.ElapsedMilliseconds);
            }
        }

        public void Walk(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            var sw = Stopwatch.StartNew();
            Trace.TraceInformation("Walking dependency graph for '{0} {1}'.", name, frameworkName);

            var context = new WalkContext();

            context.Walk(
                _loaders.OfType<IPackageLoader>(),
                name,
                version,
                frameworkName);

            context.Populate(frameworkName);

            sw.Stop();
            Trace.TraceInformation("Resolved dependencies for {0} in {1}ms", name, sw.ElapsedMilliseconds);
        }

        public DependencyExport GetDependencyExport(string name, FrameworkName targetFramework)
        {
            return _loaders.OfType<IDependencyExportResolver>()
                           .Select(r => r.GetDependencyExport(name, targetFramework))
                           .FirstOrDefault(i => i != null);
        }

        public void RefreshDependencies(string name, string version, FrameworkName targetFramework)
        {
            Walk(name, new SemanticVersion(version), targetFramework);
        }

        private AssemblyLoadResult LoadImpl(LoadContext loadContext, Stopwatch sw)
        {
            foreach (var loader in _loaders)
            {
                var loadResult = loader.Load(loadContext);

                if (loadResult != null)
                {
                    sw.Stop();

                    Trace.TraceInformation("[{0}]: Finished loading {1} in {2}ms", loader.GetType().Name, loadContext.AssemblyName, sw.ElapsedMilliseconds);

                    return loadResult;
                }
            }

            return null;
        }
    }
}
