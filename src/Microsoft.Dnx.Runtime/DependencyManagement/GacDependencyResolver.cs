// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class GacDependencyResolver : IDependencyProvider
    {
        private readonly Dictionary<string, string> _resolvedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            if (RuntimeEnvironmentHelper.IsMono || !RuntimeEnvironmentHelper.IsWindows)
            {
                return Enumerable.Empty<string>();
            }

            if (!VersionUtility.IsDesktop(targetFramework))
            {
                return Enumerable.Empty<string>();
            }

            return GetGacSearchPaths().Select(p => Path.Combine(p, "{name}", "{version}", "{name}.dll"));
        }

        public LibraryDescription GetDescription(LibraryRange libraryRange, FrameworkName targetFramework)
        {
            if (!libraryRange.IsGacOrFrameworkReference)
            {
                return null;
            }

            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                return null;
            }

            if (!VersionUtility.IsDesktop(targetFramework))
            {
                return null;
            }

            var name = libraryRange.Name;
            var version = libraryRange.VersionRange?.MinVersion;

            string path;
            if (!TryResolvePartialName(libraryRange.GetReferenceAssemblyName(), version, out path))
            {
                return null;
            }

            _resolvedPaths[name] = path;

            return new LibraryDescription(
                libraryRange,
                new LibraryIdentity(name, version, isGacOrFrameworkReference: true),
                path,
                LibraryTypes.GlobalAssemblyCache,
                Enumerable.Empty<LibraryDependency>(),
                new[] { libraryRange.GetReferenceAssemblyName() },
                framework: null);
        }

        public void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework, string runtimeIdentifier)
        {
        }

        private bool TryResolvePartialName(string name, SemanticVersion version, out string assemblyLocation)
        {
            foreach (var gacPath in GetGacSearchPaths())
            {
                var di = new DirectoryInfo(Path.Combine(gacPath, name));

                if (!di.Exists)
                {
                    continue;
                }

                foreach (var assemblyFile in di.EnumerateFiles("*.dll", SearchOption.AllDirectories))
                {
                    if (!Path.GetFileNameWithoutExtension(assemblyFile.Name).Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    SemanticVersion assemblyVersion = VersionUtility.GetAssemblyVersion(assemblyFile.FullName);
                    if (version == null || assemblyVersion == version)
                    {
                        assemblyLocation = assemblyFile.FullName;
                        return true;
                    }
                }
            }

            assemblyLocation = null;
            return false;
        }

        private static IEnumerable<string> GetGacSearchPaths()
        {
            var gacFolders = new[] { "GAC_32", "GAC_64", "GAC_MSIL" };

            string windowsFolder = Environment.GetEnvironmentVariable("WINDIR");

            foreach (var folder in gacFolders)
            {
                yield return Path.Combine(windowsFolder,
                                          "Microsoft.NET",
                                          "assembly",
                                          folder);
            }
        }
    }
}