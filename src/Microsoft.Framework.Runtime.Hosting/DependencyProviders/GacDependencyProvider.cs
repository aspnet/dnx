// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class GacDependencyProvider : IDependencyProvider
    {
        public bool SupportsType(string libraryType)
        {
            return string.Equals(libraryType, LibraryTypes.FrameworkOrGacAssembly);
        }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            //if (PlatformHelper.IsMono)
            //{
            //    return null;
            //}

            if (targetFramework.IsDesktop())
            {
                return null;
            }

            var name = libraryRange.Name;
            var version = libraryRange.VersionRange?.MinVersion;

            string path;
            if (!TryResolvePartialName(name, out path))
            {
                return null;
            }

            NuGetVersion assemblyVersion = null;//VersionUtility.GetAssemblyVersion(path);

            if (version == null || version == assemblyVersion)
            {
                var library = new Library
                {
                    LibraryRange = libraryRange,
                    Identity = new LibraryIdentity
                    {
                        Name = name,
                        Version = assemblyVersion,
                        Type = LibraryTypes.FrameworkOrGacAssembly
                    },
                    Path = path,
                    Dependencies = Enumerable.Empty<LibraryDependency>()
                };

                return library;
            }

            return null;
        }

        private bool TryResolvePartialName(string name, out string assemblyLocation)
        {
            foreach (var gacPath in GetGacSearchPaths())
            {
                var di = new DirectoryInfo(Path.Combine(gacPath, name));

                if (!di.Exists)
                {
                    continue;
                }

                var match = di.EnumerateFiles("*.dll", SearchOption.AllDirectories)
                                .FirstOrDefault(d => Path.GetFileNameWithoutExtension(d.Name).Equals(name, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    assemblyLocation = match.FullName;
                    return true;
                }
            }

            assemblyLocation = null;
            return false;
        }

        private static IEnumerable<string> GetGacSearchPaths()
        {
            var gacFolders = new[] { IntPtr.Size == 4 ? "GAC_32" : "GAC_64", "GAC_MSIL" };
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