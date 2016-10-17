// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Dnx.Runtime.Servicing;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class PackageDependencyProvider
    {
        private readonly string _packagesPath;

        private readonly IEnumerable<IPackagePathResolver> _cacheResolvers;
        private readonly IPackagePathResolver _packagePathResolver;

        public PackageDependencyProvider(string packagesPath)
        {
            _packagesPath = packagesPath;
            _cacheResolvers = GetCacheResolvers();
            _packagePathResolver = new DefaultPackagePathResolver(packagesPath);
        }

        public PackageDescription GetDescription(LockFilePackageLibrary package, LockFileTargetLibrary targetLibrary)
        {
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
            var dependencies = new List<LibraryDependency>(targetLibrary.Dependencies.Count + targetLibrary.FrameworkAssemblies.Count);
            PopulateDependencies(dependencies, targetLibrary);

            var packageDescription = new PackageDescription(
                new LibraryRange(package.Name, frameworkReference: false)
                {
                    VersionRange = new SemanticVersionRange(package.Version)
                },
                package,
                targetLibrary,
                dependencies,
                resolved,
                compatible);

            Initialize(packageDescription);

            return packageDescription;
        }

        private void PopulateDependencies(List<LibraryDependency> dependencies, LockFileTargetLibrary targetLibrary)
        {
            foreach (var d in targetLibrary.Dependencies)
            {
                dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange(d.Id, frameworkReference: false)
                    {
                        VersionRange = d.VersionSpec == null ? null : new SemanticVersionRange(d.VersionSpec)
                    }
                });
            }

            foreach (var frameworkAssembly in targetLibrary.FrameworkAssemblies)
            {
                dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange(frameworkAssembly, frameworkReference: true)
                });
            }
        }

        private void Initialize(PackageDescription package)
        {
            package.Path = ResolvePackagePath(package);

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

        // REVIEW: Should this be here? Is there a better place for this static
        public static void ResolvePackageAssemblyPaths(IEnumerable<LibraryDescription> libraries, Action<PackageDescription, AssemblyName, string> onResolveAssembly)
        {
            foreach (var library in libraries)
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
                        if (ServicingTable.TryGetReplacement(
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

                    foreach (var runtimeAssemblyPath in packageDescription.Target.ResourceAssemblies)
                    {
                        var assemblyPath = runtimeAssemblyPath.Path;
                        var name = Path.GetFileNameWithoutExtension(assemblyPath);
                        var path = Path.Combine(library.Path, assemblyPath);
                        var assemblyName = new AssemblyName(name);
                        string locale;
                        if (runtimeAssemblyPath.Properties.TryGetValue("locale", out locale))
                        {
#if DNXCORE50
                            assemblyName.CultureName = locale;
#elif DNX451
                            assemblyName.CultureInfo = new CultureInfo(locale);
#else
#error Unhandled target framework
#endif
                        }

                        string replacementPath;
                        if (ServicingTable.TryGetReplacement(
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

        public static Dictionary<AssemblyName, string> ResolvePackageAssemblyPaths(IEnumerable<LibraryDescription> libraries)
        {
            var assemblies = new Dictionary<AssemblyName, string>(AssemblyNameComparer.OrdinalIgnoreCase);

            ResolvePackageAssemblyPaths(libraries, (package, assemblyName, path) =>
            {
                assemblies[assemblyName] = path;
            });

            return assemblies;
        }

        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), "_._", StringComparison.Ordinal);
        }

        public static string ResolveRepositoryPath(string rootDirectory)
        {
            // Order
            // 1. global.json { "packages": "..." }
            // 2. EnvironmentNames.Packages environment variable
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. {DefaultLocalRuntimeHomeDir}\packages

            GlobalSettings settings;
            if (GlobalSettings.TryGetGlobalSettings(rootDirectory, out settings) &&
                !string.IsNullOrEmpty(settings.PackagesPath))
            {
                return Path.Combine(rootDirectory, settings.PackagesPath);
            }

            var runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.Packages);

            if (string.IsNullOrEmpty(runtimePackages))
            {
                runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.DnxPackages);
            }

            if (!string.IsNullOrEmpty(runtimePackages))
            {
                return runtimePackages;
            }

            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            return Path.Combine(profileDirectory, Constants.DefaultLocalRuntimeHomeDir, "packages");
        }

#if DNX451
        public static void EnableLoadingNativeLibraries(IEnumerable<LibraryDescription> libraries)
        {
            EnableLoadingNativeLibraries(libraries.OfType<PackageDescription>());
            EnableLoadingNativeLibraries(libraries.OfType<ProjectDescription>());
        }

        public static void EnableLoadingNativeLibraries(IEnumerable<PackageDescription> packages)
        {
            var existingPath = new HashSet<string>(Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator));
            var nativeLibPaths = new StringBuilder();
            foreach (var packageDescription in packages)
            {
                foreach (var nativeLib in packageDescription.Target.NativeLibraries)
                {
                    var nativeLibFullPath = Path.Combine(packageDescription.Path, nativeLib.Path);

                    if (RuntimeEnvironmentHelper.IsWindows)
                    {
                        var newPath = Path.GetDirectoryName(nativeLibFullPath);
                        if (existingPath.Add(newPath))
                        {
                            nativeLibPaths.Append(";").Append(newPath);
                        }
                    }
                    else
                    {
                        PreLoadNativeLib(nativeLibFullPath);
                    }
                }

                if (nativeLibPaths.Length > 0)
                {
                    var nativeLibsPathsStr = nativeLibPaths.ToString();
                    Logger.TraceInformation("[{0}]: Enabling loading native libraries from packages by extendig %PATH% with: {1}",
                        nameof(PackageDependencyProvider), nativeLibsPathsStr);

                    var path = Environment.GetEnvironmentVariable("PATH");
                    Environment.SetEnvironmentVariable("PATH", path + nativeLibsPathsStr);
                }
            }
        }

        public static void EnableLoadingNativeLibraries(IEnumerable<ProjectDescription> projects)
        {
            var folderCandidates = NativeLibPathUtils.GetNativeSubfolderCandidates(RuntimeEnvironmentHelper.RuntimeEnvironment);

            var nativeLibPaths = new StringBuilder();
            foreach (var projectDescription in projects)
            {
                foreach (var folder in folderCandidates)
                {
                    var path = Path.Combine(Path.GetDirectoryName(projectDescription.Path), "runtimes", folder, "native");

                    if (Directory.Exists(path))
                    {
                        if (RuntimeEnvironmentHelper.IsWindows)
                        {
                            nativeLibPaths.Append(";").Append(path);
                        }
                        else
                        {
                            foreach (var nativeLibFullPath in Directory.EnumerateFiles(path))
                            {
                                PreLoadNativeLib(nativeLibFullPath);
                            }
                        }
                    }
                }
            }

            if (nativeLibPaths.Length > 0)
            {
                var nativeLibPathsStr = nativeLibPaths.ToString();
                Logger.TraceInformation("[{0}]: Enabling loading native libraries from projects by extendig %PATH% with: {1}",
                    nameof(PackageDependencyProvider), nativeLibPathsStr);

                var path = Environment.GetEnvironmentVariable("PATH");
                Environment.SetEnvironmentVariable("PATH", path + nativeLibPathsStr);
            }
        }

        private static void PreLoadNativeLib(string nativeLibFullPath)
        {
            Debug.Assert(RuntimeEnvironmentHelper.IsMono, "Mono specific");

            // Preloading does not work on Mono on Linux. As a workaround the user can set the LD_LIBRARY_PATH.
            var runtimeEnvironment = RuntimeEnvironmentHelper.RuntimeEnvironment;
            if (runtimeEnvironment.OperatingSystem == RuntimeOperatingSystems.Linux && RuntimeEnvironmentHelper.IsMono)
            {
                return;
            }

            Logger.TraceInformation("[{0}]: Attempting to preload: {1}", nameof(PackageDependencyProvider), nativeLibFullPath);

            var handle = dlopen(nativeLibFullPath, RTLD_GLOBAL | RTLD_LAZY);

            Logger.TraceInformation("[{0}]: Preloading: {1} {2}", nameof(PackageDependencyProvider), nativeLibFullPath,
                handle != IntPtr.Zero ? "succeeded" : "failed");
        }
#endif

#if DNX451
        [DllImport("libdl")]
        public static extern IntPtr dlopen(string fileName, int flags);

        const int RTLD_LAZY = 0x01;
        const int RTLD_GLOBAL = 0x08;
#endif

        private static IEnumerable<IPackagePathResolver> GetCacheResolvers()
        {
            var packageCachePathValue = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesCache);

            if (string.IsNullOrEmpty(packageCachePathValue))
            {
                return Enumerable.Empty<IPackagePathResolver>();
            }

            return packageCachePathValue.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(path => new DefaultPackagePathResolver(path));
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
