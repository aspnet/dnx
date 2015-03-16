using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Dependencies;
using Microsoft.Framework.Runtime.Internal;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Loader
{
    /// <summary>
    /// Loads .NET Assemblies from NuGet Packages
    /// </summary>
    /// <remarks>
    /// This loader REQUIRES that a lock file has been generated for the project.
    /// </remarks>
    public class PackageAssemblyLoader : IAssemblyLoader
    {
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private readonly ILogger Log;
        private readonly Dictionary<string, string> _assemblyLookupTable;

        public PackageAssemblyLoader(IAssemblyLoadContextAccessor loadContextAccessor,
            LockFile lockFile,
            NuGetFramework targetFramework,
            DefaultPackagePathResolver pathResolver)
        {
            _loadContextAccessor = loadContextAccessor;

            Log = RuntimeLogging.Logger<PackageAssemblyLoader>();

            // Initialize the assembly lookup table
            _assemblyLookupTable = InitializeAssemblyLookup(lockFile, targetFramework, pathResolver);
        }

        /// <summary>
        /// Loads an assembly from a NuGet package
        /// </summary>
        /// <remarks>
        /// Note that the Package name is never used when loading assemblies.
        /// If multiple packages provide the same assembly name, it is UNDEFINED
        /// which assembly will be loaded.
        /// </remarks>
        /// <param name="name">The name of the assembly to load</param>
        /// <returns>An <see cref="Assembly"/>, or null if the assembly could not be found</returns>
        public Assembly Load(string name)
        {
            return Load(name, _loadContextAccessor.Default);
        }

        private Assembly Load(string name, IAssemblyLoadContext loadContext)
        {
            Log.LogVerbose($"Requested load of {name}");

            string assemblyLocation;
            if(_assemblyLookupTable.TryGetValue(name, out assemblyLocation))
            {
                return loadContext.LoadFile(assemblyLocation);
            }
            return null;
        }

        private Dictionary<string, string> InitializeAssemblyLookup(LockFile lockFile, NuGetFramework targetFramework, DefaultPackagePathResolver pathResolver)
        {
            var lookup = new Dictionary<string, string>();
            var cacheResolvers = GetCacheResolvers();
            foreach(var lib in lockFile.Libraries)
            {
                Log.LogDebug($"Scanning library {lib.Name} {lib.Version}");
                var group = lib.FrameworkGroups.FirstOrDefault(f => f.TargetFramework.Equals(targetFramework));
                if(group != null)
                {
                    foreach(var assembly in group.RuntimeAssemblies)
                    {
                        Log.LogDebug($"Locating {assembly} in {lib.Name} {lib.Version}");
                        string asmName = Path.GetFileNameWithoutExtension(assembly);
                        if(Log.IsEnabled(LogLevel.Warning) && lookup.ContainsKey(asmName))
                        {
                            Log.LogWarning($"{asmName} already exists at {lookup[asmName]}. Overriding!");
                        }

                        // Locate the package
                        var packageRoot = ResolvePackagePath(pathResolver, cacheResolvers, lib);

                        // Resolve the assembly path
                        var assemblyLocation = Path.Combine(packageRoot, assembly);

                        lookup[asmName] = assemblyLocation;
                    }
                }
                else
                {
                    Log.LogDebug($"No assemblies in {lib.Name} {lib.Version} for {targetFramework}");
                }
            }

            return lookup;
        }

        private string ResolvePackagePath(DefaultPackagePathResolver defaultResolver,
                                          IEnumerable<DefaultPackagePathResolver> cacheResolvers,
                                          LockFileLibrary lib)
        {
            string expectedHash = lib.Sha;

            foreach (var resolver in cacheResolvers)
            {
                var cacheHashFile = resolver.GetHashPath(lib.Name, lib.Version);

                // REVIEW: More efficient compare?
                if (File.Exists(cacheHashFile) &&
                    File.ReadAllText(cacheHashFile) == expectedHash)
                {
                    return resolver.GetInstallPath(lib.Name, lib.Version);
                }
            }

            return defaultResolver.GetInstallPath(lib.Name, lib.Version);
        }

        private static IEnumerable<DefaultPackagePathResolver> GetCacheResolvers()
        {
            var packageCachePathValue = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesCache);

            if (string.IsNullOrEmpty(packageCachePathValue))
            {
                return Enumerable.Empty<DefaultPackagePathResolver>();
            }

            return packageCachePathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(path => new DefaultPackagePathResolver(path));
        }

    }
}