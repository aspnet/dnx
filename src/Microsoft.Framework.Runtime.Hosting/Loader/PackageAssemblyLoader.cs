using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Dependencies;
using Microsoft.Framework.Runtime.Internal;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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
            IEnumerable<Library> libraries,
            NuGetFramework targetFramework,
            DefaultPackagePathResolver pathResolver)
        {
            _loadContextAccessor = loadContextAccessor;

            Log = RuntimeLogging.Logger<PackageAssemblyLoader>();

            // Initialize the assembly lookup table
            _assemblyLookupTable = InitializeAssemblyLookup(libraries, targetFramework, pathResolver);
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

        private Dictionary<string, string> InitializeAssemblyLookup(IEnumerable<Library> libraries, NuGetFramework targetFramework, DefaultPackagePathResolver pathResolver)
        {
            var lookup = new Dictionary<string, string>();
            var cacheResolvers = GetCacheResolvers();
            foreach(var library in libraries)
            {
                Debug.Assert(library.Identity.Type == LibraryTypes.Package);

                Log.LogDebug($"Scanning library {library.Identity.Name} {library.Identity.Version}");
                var lockFileLib = library.GetRequiredItem<LockFileLibrary>(KnownLibraryProperties.LockFileLibrary);
                var group = lockFileLib.FirstOrDefault(f => f.TargetFramework.Equals(targetFramework));
                if(group != null)
                {
                    foreach(var assembly in group.RuntimeAssemblies)
                    {
                        Log.LogDebug($"Locating {assembly} in {library.Identity.Name} {library.Identity.Version}");
                        string asmName = Path.GetFileNameWithoutExtension(assembly);
                        if(Log.IsEnabled(LogLevel.Warning) && lookup.ContainsKey(asmName))
                        {
                            Log.LogWarning($"{asmName} already exists at {lookup[asmName]}. Overriding!");
                        }

                        // Locate the package
                        var packageRoot = ResolvePackagePath(pathResolver, cacheResolvers, lockFileLib);

                        // Resolve the assembly path
                        var assemblyLocation = Path.Combine(packageRoot, assembly);

                        lookup[asmName] = assemblyLocation;
                    }
                }
                else
                {
                    Log.LogDebug($"No assemblies in {library.Identity.Name} {library.Identity.Version} for {targetFramework}");
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