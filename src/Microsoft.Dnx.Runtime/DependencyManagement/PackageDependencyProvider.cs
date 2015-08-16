using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime
{

    public class PackageDependencyProvider : IDependencyProvider
    {
        private readonly string _packagesPath;
        private readonly IDictionary<Tuple<string, FrameworkName, string>, LockFileTargetLibrary> _lookup;
        private readonly IDictionary<Tuple<string, SemanticVersion>, LockFilePackageLibrary> _packages;

        private readonly IEnumerable<IPackagePathResolver> _cacheResolvers;
        private readonly IPackagePathResolver _packagePathResolver;

        public PackageDependencyProvider(string packagesPath, LockFile lockFile)
        {
            _packagesPath = packagesPath;
            _cacheResolvers = GetCacheResolvers();
            _packagePathResolver = new DefaultPackagePathResolver(packagesPath);

            _lookup = new Dictionary<Tuple<string, FrameworkName, string>, LockFileTargetLibrary>();
            _packages = new Dictionary<Tuple<string, SemanticVersion>, LockFilePackageLibrary>();

            foreach (var t in lockFile.Targets)
            {
                foreach (var library in t.Libraries)
                {
                    // Each target has a single package version per id
                    _lookup[Tuple.Create(t.RuntimeIdentifier, t.TargetFramework, library.Name)] = library;
                }
            }

            foreach (var library in lockFile.PackageLibraries)
            {
                _packages[Tuple.Create(library.Name, library.Version)] = library;
            }
        }

        // REVIEW: Should this be here? Is there a better place for this static
        public static void ResolvePackageAssemblyPaths(LibraryManager libraryManager, Action<PackageDescription, AssemblyName, string> onResolveAssembly)
        {
            foreach (var library in libraryManager.GetLibraryDescriptions())
            {
                if (library.Type == LibraryTypes.Package)
                {
                    var packageDescription = (PackageDescription)library;

                    foreach (var runtimeAssemblyPath in packageDescription.Target.RuntimeAssemblies)
                    {
                        var assemblyPath = runtimeAssemblyPath.Path;
                        var name = Path.GetFileNameWithoutExtension(assemblyPath);
                        var path = Path.Combine(library.Path, assemblyPath);
                        var assemblyName = new AssemblyName(name);

                        string replacementPath;
                        if (Servicing.ServicingTable.TryGetReplacement(
                            library.Identity.Name,
                            library.Identity.Version,
                            assemblyPath,
                            out replacementPath))
                        {
                            onResolveAssembly(packageDescription, assemblyName, replacementPath);
                        }
                        else
                        {
                            onResolveAssembly(packageDescription, assemblyName, path);
                        }
                    }
                }
            }
        }

        public static Dictionary<AssemblyName, string> ResolvePackageAssemblyPaths(LibraryManager libraryManager)
        {
            var assemblies = new Dictionary<AssemblyName, string>(AssemblyNameComparer.OrdinalIgnoreCase);

            ResolvePackageAssemblyPaths(libraryManager, (package, assemblyName, path) =>
            {
                assemblies[assemblyName] = path;
            });

            return assemblies;
        }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return new[]
            {
                Path.Combine(_packagesPath, "{name}", "{version}")
            };
        }

        public LibraryDescription GetDescription(LibraryRange libraryRange, FrameworkName targetFramework)
        {
            if (libraryRange.IsGacOrFrameworkReference)
            {
                return null;
            }

            var targetKey = Tuple.Create((string)null, targetFramework, libraryRange.Name);

            LockFileTargetLibrary targetLibrary;
            if (_lookup.TryGetValue(targetKey, out targetLibrary))
            {
                var packageKey = Tuple.Create(targetLibrary.Name, targetLibrary.Version);
                var package = _packages[packageKey];

                // If a NuGet dependency is supposed to provide assemblies but there is no assembly compatible with
                // current target framework, we should mark this dependency as unresolved
                var containsAssembly = package.Files
                    .Any(x => x.StartsWith($"ref{Path.DirectorySeparatorChar}") ||
                        x.StartsWith($"lib{Path.DirectorySeparatorChar}"));

                var compatible = targetLibrary.FrameworkAssemblies.Any() ||
                    targetLibrary.CompileTimeAssemblies.Any() ||
                    targetLibrary.RuntimeAssemblies.Any() ||
                    !containsAssembly;

                var resolved = compatible;


                var packageDescription = new PackageDescription(
                    libraryRange,
                    package,
                    targetLibrary,
                    GetDependencies(targetLibrary),
                    resolved,
                    compatible);

                Initialize(packageDescription);

                return packageDescription;
            }

            return null;
        }

        private IEnumerable<LibraryDependency> GetDependencies(LockFileTargetLibrary targetLibrary)
        {
            foreach (var d in targetLibrary.Dependencies)
            {
                yield return new LibraryDependency
                {
                    LibraryRange = new LibraryRange(d.Id, frameworkReference: false)
                    {
                        VersionRange = d.VersionSpec == null ? null : new SemanticVersionRange(d.VersionSpec)
                    }
                };
            }

            foreach (var frameworkAssembly in targetLibrary.FrameworkAssemblies)
            {
                yield return new LibraryDependency
                {
                    LibraryRange = new LibraryRange(frameworkAssembly, frameworkReference: true)
                };
            }
        }

        public void Initialize(PackageDescription package)
        {
            string packagePath = ResolvePackagePath(package);

            // If the package path doesn't exist then mark this dependency as unresolved
            if (!Directory.Exists(packagePath))
            {
                package.Resolved = false;
            }

            package.Path = packagePath;

            if (Servicing.Breadcrumbs.Instance.IsPackageServiceable(package))
            {
                Servicing.Breadcrumbs.Instance.AddBreadcrumb(package.Identity.Name, package.Identity.Version);
            }

            var assemblies = new List<string>();

            foreach (var runtimeAssemblyPath in package.Target.RuntimeAssemblies)
            {
                if (IsPlaceholderFile(runtimeAssemblyPath))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(runtimeAssemblyPath);
                assemblies.Add(name);
            }

            package.Assemblies = assemblies;
        }

        private string ResolvePackagePath(PackageDescription package)
        {
            string expectedHash = package.Library.Sha512;

            foreach (var resolver in _cacheResolvers)
            {
                var cacheHashFile = resolver.GetHashPath(package.Identity.Name, package.Identity.Version);

                // REVIEW: More efficient compare?
                if (File.Exists(cacheHashFile) &&
                    File.ReadAllText(cacheHashFile) == expectedHash)
                {
                    return resolver.GetInstallPath(package.Identity.Name, package.Identity.Version);
                }
            }

            return _packagePathResolver.GetInstallPath(package.Identity.Name, package.Identity.Version);
        }

        private static IEnumerable<IPackagePathResolver> GetCacheResolvers()
        {
            var packageCachePathValue = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesCache);

            if (string.IsNullOrEmpty(packageCachePathValue))
            {
                return Enumerable.Empty<IPackagePathResolver>();
            }

            return packageCachePathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(path => new DefaultPackagePathResolver(path));
        }

        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), "_._", StringComparison.Ordinal);
        }

        public static string ResolveRepositoryPath(string rootDirectory)
        {
            // Order
            // 1. EnvironmentNames.Packages environment variable
            // 2. global.json { "packages": "..." }
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. {DefaultLocalRuntimeHomeDir}\packages

            var runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.Packages);

            if (string.IsNullOrEmpty(runtimePackages))
            {
                runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.DnxPackages);
            }

            if (!string.IsNullOrEmpty(runtimePackages))
            {
                return runtimePackages;
            }

            GlobalSettings settings;
            if (GlobalSettings.TryGetGlobalSettings(rootDirectory, out settings) &&
                !string.IsNullOrEmpty(settings.PackagesPath))
            {
                return Path.Combine(rootDirectory, settings.PackagesPath);
            }

            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            return Path.Combine(profileDirectory, Constants.DefaultLocalRuntimeHomeDir, "packages");
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public static IEqualityComparer<AssemblyName> OrdinalIgnoreCase = new AssemblyNameComparer();

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                return
                    string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.CultureName ?? "", y.CultureName ?? "", StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(AssemblyName obj)
            {
                var hashCode = 0;
                if (obj.Name != null)
                {
                    hashCode ^= obj.Name.ToUpperInvariant().GetHashCode();
                }

                hashCode ^= (obj.CultureName?.ToUpperInvariant() ?? "").GetHashCode();
                return hashCode;
            }
        }
    }
}
