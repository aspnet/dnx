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
    /// Loads library assemblies from NuGet Packages
    /// </summary>
    /// <remarks>
    /// This loader REQUIRES that a lock file has been generated for the project.
    /// </remarks>
    public class PackageAssemblyLoader : IAssemblyLoader
    {
        private readonly ILogger Log;
        private readonly Dictionary<string, string> _assemblyLookupTable;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public PackageAssemblyLoader(IEnumerable<Library> libraries, NuGetFramework runtimeFramework, DefaultPackagePathResolver pathResolver, IAssemblyLoadContextAccessor loadContextAccessor)
        {
            Log = RuntimeLogging.Logger<PackageAssemblyLoader>();
            _loadContextAccessor = loadContextAccessor;

            _assemblyLookupTable = InitializeAssemblyLookupTable(libraries, runtimeFramework, pathResolver);
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
            if (_assemblyLookupTable.TryGetValue(name, out assemblyLocation))
            {
                using (Log.LogTimed("Loading Assembly"))
                {
                    return loadContext.LoadFile(assemblyLocation);
                }
            }
            return null;
        }

        public Dictionary<string, string> InitializeAssemblyLookupTable(IEnumerable<Library> libraries, NuGetFramework runtimeFramework, DefaultPackagePathResolver pathResolver)
        {
            using (Log.LogTimedMethod())
            {
                Log.LogInformation("Scanning resolved Package libraries for assemblies");
                var lookup = new Dictionary<string, string>();
                var cacheResolvers = GetCacheResolvers();
                foreach (var library in libraries)
                {
                    Debug.Assert(library.Identity.Type == LibraryTypes.Package);

                    Log.LogDebug($"Scanning library {library.Identity.Name} {library.Identity.Version}");
                    var lockFileLib = library.GetRequiredItem<LockFileLibrary>(KnownLibraryProperties.LockFileLibrary);
                    var lockFileFrameworkGroup = library.GetItem<LockFileFrameworkGroup>(KnownLibraryProperties.LockFileFrameworkGroup);
                    if (lockFileFrameworkGroup != null)
                    {
                        foreach (var assembly in lockFileFrameworkGroup.RuntimeAssemblies)
                        {
                            Log.LogDebug($"Locating {assembly} in {library.Identity.Name} {library.Identity.Version}");
                            string asmName = Path.GetFileNameWithoutExtension(assembly);
                            if (Log.IsEnabled(LogLevel.Warning) && lookup.ContainsKey(asmName))
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
                        Log.LogDebug($"No assemblies in {library.Identity.Name} {library.Identity.Version} for {runtimeFramework}");
                    }
                }

                return lookup;
            }
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
