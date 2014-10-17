using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using Microsoft.Framework.Runtime;
using NuGet;

namespace DependencyAnalyzer
{
    public class Program
    {
        private readonly IApplicationEnvironment _env;
        private string _runtimePath;

        public Program(IApplicationEnvironment env)
        {
            _env = env;
        }

        public void Main(string[] args)
        {
            var accessor = new CacheContextAccessor();
            var cache = new Cache(accessor);

            var deps = new Dictionary<string, HashSet<string>>();

            if (args.Length == 0)
            {
                Console.WriteLine("Runtime path is required");
                return;
            }

            _runtimePath = args[0];

            var runtimeProjects = new[] {
                "Microsoft.Framework.Runtime.Roslyn",
                "Microsoft.Framework.ApplicationHost",
                "klr.host",
                "klr.core45.managed"
            };

            deps["Runtime"] = new HashSet<string>();

            foreach (var name in runtimeProjects)
            {
                deps["Runtime"].AddRange(GetContractDependencies(accessor, cache, name));
            }

            foreach (var name in new[] { "Microsoft.Framework.DesignTimeHost",
                                         "Microsoft.Framework.PackageManager",
                                         "Microsoft.Framework.Project" })
            {
                deps[name] = GetContractDependencies(accessor, cache, name);
            }

            foreach (var pair in deps.Skip(1))
            {
                pair.Value.ExceptWith(deps["Runtime"]);
            }

            TextWriter writer = args.Length == 1 ? Console.Out : new StreamWriter(args[1]);

            foreach (var root in deps)
            {
                writer.WriteLine("-" + root.Key);
                foreach (var contract in root.Value)
                {
                    writer.WriteLine(contract);
                }
            }

            writer.Write("-");
            writer.Flush();
        }

        private HashSet<string> GetContractDependencies(CacheContextAccessor accessor, Cache cache, string name)
        {
            var used = new HashSet<string>();

            var dir = Path.GetDirectoryName(_env.ApplicationBasePath);

            var path = Path.Combine(dir, name);

            var framework = VersionUtility.ParseFrameworkName("aspnetcore50");

            var hostContext = new ApplicationHostContext(
                                serviceProvider: null,
                                projectDirectory: path,
                                packagesDirectory: null,
                                configuration: "Debug",
                                targetFramework: framework,
                                cache: cache,
                                cacheContextAccessor: accessor,
                                namedCacheDependencyProvider: new NamedCacheDependencyProvider());

            hostContext.DependencyWalker.Walk(hostContext.Project.Name, hostContext.Project.Version, framework);

            var manager = (ILibraryManager)hostContext.ServiceProvider.GetService(typeof(ILibraryManager));

            var packageAssembles = new PackageAssembly();

            foreach (var library in manager.GetLibraries())
            {
                foreach (var assemblyName in library.LoadableAssemblies)
                {
                    used.Add(assemblyName.Name);

                    PackageAssembly assembly;
                    if (hostContext.NuGetDependencyProvider.PackageAssemblyLookup.TryGetValue(assemblyName.Name, out assembly))
                    {
                        used.AddRange(WalkAll(assembly.Path));
                    }
                }
            }

            return used;
        }

        private IList<string> WalkAll(string rootPath)
        {
            var set = new HashSet<string>();
            
            var stack = new Stack<string>();
            stack.Push(rootPath);
            while (stack.Count > 0)
            {
                var path = stack.Pop();

                if (!set.Add(Path.GetFileNameWithoutExtension(path)))
                {
                    continue;
                }

                foreach (var reference in GetReferences(path))
                {
                    var newPath = Path.Combine(_runtimePath, reference + ".dll");

                    if (!File.Exists(newPath))
                    {
                        continue;
                    }

                    stack.Push(newPath);
                }
            }

            return set.ToList();

        }

        private static IList<string> GetReferences(string path)
        {
            var references = new List<string>();

            using (var stream = File.OpenRead(path))
            {
                var peReader = new PEReader(stream);

                var reader = peReader.GetMetadataReader();

                foreach (var a in reader.AssemblyReferences)
                {
                    var reference = reader.GetAssemblyReference(a);
                    var referenceName = reader.GetString(reference.Name);

                    references.Add(referenceName);
                }

                return references;
            }
        }
    }
}
