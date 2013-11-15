using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NuGet;

namespace Loader
{
    public class AssemblyLoader : IAssemblyLoader
    {
        private List<IAssemblyLoader> _loaders = new List<IAssemblyLoader>();

        private readonly ConcurrentDictionary<string, Assembly> _cache = new ConcurrentDictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        public void Add(IAssemblyLoader loader)
        {
            _loaders.Add(loader);
        }

        public Assembly Load(LoadOptions options)
        {
            // REVIEW: Can we skip resource assemblies?
            if (options.AssemblyName.EndsWith("resources"))
            {
                return null;
            }

            var sw = new Stopwatch();
            sw.Start();
            Trace.TraceInformation("Loading {0}", options.AssemblyName);

            Assembly asm;

            if (!_cache.TryGetValue(options.AssemblyName, out asm))
            {
                asm = LoadImpl(options, sw);

                if (asm != null)
                {
                    _cache.TryAdd(options.AssemblyName, asm);
                }
            }
            else
            {
                sw.Stop();
                Trace.TraceInformation("[Cache]: Loaded {0} in {1}ms", options.AssemblyName, sw.ElapsedMilliseconds);
            }

            return asm;
        }

        public void Walk(string name, SemanticVersion version)
        {
            var sw = Stopwatch.StartNew();
            Trace.TraceInformation("Walking dependency graph for '{0}'.", name);

            var context = new WalkContext();
            WalkRecursive(context, name, version);
            context.Populate();

            sw.Stop();
            Trace.TraceInformation("Resolved dependencies for {0} in {1}ms", name, sw.ElapsedMilliseconds);
        }

        private void WalkRecursive(WalkContext context, string name, SemanticVersion version)
        {
            var hit = _loaders
                .OfType<IDependencyResolver>()
                .Select(x => new
                {
                    Resolver = x,
                    Dependencies = x.GetDependencies(name, version)
                })
                .FirstOrDefault(x => x.Dependencies != null);

            if (hit == null)
            {
                return;
            }

            var d = new Dependency
            {
                Name = name,
                Version = version
            };

            if (context.TryPush(d, hit.Resolver))
            {
                foreach (var dependency in hit.Dependencies)
                {
                    WalkRecursive(context, dependency.Name, dependency.Version);
                }

                context.Pop(d);
            }
        }

        public void Attach(AppDomain appDomain)
        {
            appDomain.AssemblyResolve += OnAssemblyResolve;
        }

        public void Detach(AppDomain appDomain)
        {
            appDomain.AssemblyResolve -= OnAssemblyResolve;
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var an = new AssemblyName(args.Name);

            var options = new LoadOptions
            {
                AssemblyName = an.Name,
            };

            return Load(options);
        }

        private Assembly LoadImpl(LoadOptions options, Stopwatch sw)
        {
            foreach (var loader in _loaders)
            {
                var assembly = loader.Load(options);

                if (assembly != null)
                {
                    sw.Stop();

                    Trace.TraceInformation("[{0}]: Finished loading {1} in {2}ms", loader.GetType().Name, options.AssemblyName, sw.ElapsedMilliseconds);

                    return assembly;
                }
            }

            return null;
        }
    }

    public class WalkContext
    {
        private readonly IDictionary<string, Node> _dependencies = new Dictionary<string, Node>();
        private readonly Stack<Dependency> _stack = new Stack<Dependency>();

        public bool TryPush(Dependency dependency, IDependencyResolver resolver)
        {
            Node existingNode;
            if (_dependencies.TryGetValue(dependency.Name, out existingNode))
            {
                // First visit to D: A -> B -> D[1.0] -> E[1.0] -> Q[1.0]
                // Second visit to D: A -> C -> D[2.0] -> F[1.0]
                // any knowledge of E[1.0] and below is not applicable

                //TODO: maintain graph of walk to ignore all prior knowledge
                if (dependency.Version != null && existingNode.Dependency.Version != null && existingNode.Dependency.Version > dependency.Version)
                {
                    return false;
                }
            }

            _dependencies[dependency.Name] = new Node
            {
                Dependency = dependency,
                Resolver = resolver
            };

            if (_stack.Any(s => s.Name == dependency.Name))
            {
                return false;
            }

            _stack.Push(dependency);
            return true;
        }

        public void Pop(Dependency dependency)
        {
            var popped = _stack.Pop();

            if (popped != dependency)
            {
                throw new Exception("Unequal calls. AssemblyLoader busted.");
            }
        }

        public void Populate()
        {
            foreach (var groupByResolver in _dependencies.GroupBy(x => x.Value.Resolver))
            {
                var dependencyResolver = groupByResolver.Key;
                var dependencies = groupByResolver.Select(x => x.Value.Dependency).ToList();

                Trace.TraceInformation("[{0}]: " + String.Join(", ", dependencies), dependencyResolver.GetType().Name);

                dependencyResolver.Initialize(dependencies);
            }
        }

        private class Node
        {
            public Dependency Dependency;
            public IDependencyResolver Resolver;
        }
    }
}
