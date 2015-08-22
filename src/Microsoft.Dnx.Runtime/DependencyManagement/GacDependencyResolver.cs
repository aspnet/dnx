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
    public class GacDependencyResolver
    {
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
            if (!TryResolvePartialName(libraryRange.GetReferenceAssemblyName(), version, targetFramework, out path))
            {
                return null;
            }

            return new LibraryDescription(
                libraryRange,
                new LibraryIdentity(name, version, isGacOrFrameworkReference: true),
                path,
                LibraryTypes.GlobalAssemblyCache,
                Enumerable.Empty<LibraryDependency>(),
                new[] { libraryRange.GetReferenceAssemblyName() },
                framework: targetFramework);
        }

        private bool TryResolvePartialName(string name, SemanticVersion version, FrameworkName targetFramework, out string assemblyLocation)
        {
            foreach (var gacPath in GetGacSearchPaths(targetFramework))
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

        private static IEnumerable<string> GetGacSearchPaths(FrameworkName targetFramework)
        {
            var gacFolders = new[] { "GAC_32", "GAC_64", "GAC_MSIL" };

            string windowsFolder = Environment.GetEnvironmentVariable("WINDIR");

            string gacRoot;
            if (targetFramework.Version.Major < 4)
            {
                // Old GAC root
                gacRoot = Path.Combine(windowsFolder, "assembly");
            }
            else
            {
                // New GAC root
                gacRoot = Path.Combine(windowsFolder, "Microsoft.NET", "assembly");
            }

            foreach (var folder in gacFolders)
            {
                yield return Path.Combine(gacRoot, folder);
            }
        }
    }
}