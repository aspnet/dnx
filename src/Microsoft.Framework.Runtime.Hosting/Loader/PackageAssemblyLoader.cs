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
    /// Creates <see cref="PackageAssemblyLoader"/> objects based on the specified
    /// package path resolver.
    /// </summary>
    public class PackageAssemblyLoaderFactory : IAssemblyLoaderFactory
    {
        private readonly PackagePathResolver _packagePathResolver;

        public PackageAssemblyLoaderFactory(PackagePathResolver packagePathResolver)
        {
            _packagePathResolver = packagePathResolver;
        }

        public IAssemblyLoader Create(NuGetFramework runtimeFramework, IAssemblyLoadContextAccessor loadContextAccessor, DependencyManager dependencies)
        {
            return new PackageAssemblyLoader(
                runtimeFramework,
                loadContextAccessor,
                dependencies.GetLibraries(LibraryTypes.Package),
                _packagePathResolver);
        }
    }

    /// <summary>
    /// Loads library assemblies from NuGet Packages
    /// </summary>
    /// <remarks>
    /// This loader REQUIRES that a lock file has been generated for the project.
    /// </remarks>
    public class PackageAssemblyLoader : IAssemblyLoader
    {
        private readonly ILogger Log;
        private readonly Dictionary<AssemblyName, string> _assemblyLookupTable;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public PackageAssemblyLoader(NuGetFramework runtimeFramework, IAssemblyLoadContextAccessor loadContextAccessor, IEnumerable<Library> libraries, PackagePathResolver pathResolver)
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
            return Load(new AssemblyName(name), _loadContextAccessor.Default);
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
        public Assembly Load(AssemblyName name)
        {
            return Load(name, _loadContextAccessor.Default);
        }

        private Assembly Load(AssemblyName name, IAssemblyLoadContext loadContext)
        {
            string assemblyLocation;
            if (_assemblyLookupTable.TryGetValue(name, out assemblyLocation))
            {
                using (Log.LogTimedMethod())
                {
                    Log.LogVerbose($"Requested load of {name}");

                    return loadContext.LoadFile(assemblyLocation);
                }
            }
            return null;
        }

        private Dictionary<AssemblyName, string> InitializeAssemblyLookupTable(IEnumerable<Library> libraries, NuGetFramework runtimeFramework, PackagePathResolver pathResolver)
        {
            using (Log.LogTimedMethod())
            {
                Log.LogInformation("Scanning resolved Package libraries for assemblies");
                var lookup = new Dictionary<AssemblyName, string>();
                foreach (var library in libraries)
                {
                    Debug.Assert(library.Identity.Type == LibraryTypes.Package);

                    Log.LogDebug($"Scanning library {library.Identity.Name} {library.Identity.Version}");
                    var lockFileLib = library.GetRequiredItem<LockFileLibrary>(KnownLibraryProperties.LockFileLibrary);
                    var lockFileFrameworkGroup = library.GetItem<LockFileFrameworkGroup>(KnownLibraryProperties.LockFileFrameworkGroup);
                    if (lockFileFrameworkGroup != null)
                    {
                        foreach (var assemblyPath in lockFileFrameworkGroup.RuntimeAssemblies)
                        {
                            Log.LogDebug($"Locating {assemblyPath} in {library.Identity.Name} {library.Identity.Version}");
                            var name = Path.GetFileNameWithoutExtension(assemblyPath);
                            var assemblyName = new AssemblyName(name);
                            if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                            {
                                var cultureName = Path.GetFileName(Path.GetDirectoryName(assemblyPath));
#if DNXCORE50
                                assemblyName.CultureName = cultureName;
#elif DNX451
                                assemblyName.CultureInfo = new System.Globalization.CultureInfo(cultureName);
#else
#error Unhandled target framework
#endif
                            }

                            if (Log.IsEnabled(LogLevel.Warning) && lookup.ContainsKey(assemblyName))
                            {
                                Log.LogWarning($"{assemblyName} already exists at {lookup[assemblyName]}. Overriding!");
                            }

                            // Locate the package
                            var packageRoot = pathResolver.ResolvePackagePath(lockFileLib.Sha, lockFileLib.Name, lockFileLib.Version);

                            // Resolve the assembly path
                            var assemblyLocation = Path.Combine(packageRoot, assemblyPath);

                            lookup[assemblyName] = assemblyLocation;
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
    }
}
