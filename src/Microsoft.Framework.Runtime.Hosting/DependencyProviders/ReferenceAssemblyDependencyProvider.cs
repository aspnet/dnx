// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class ReferenceAssemblyDependencyProvider : IDependencyProvider
    {
        public bool SupportsType(string libraryType)
        {
            return string.Equals(libraryType, LibraryTypes.FrameworkOrGacAssembly);
        }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var name = libraryRange.Name;
            var version = libraryRange.VersionRange?.MinVersion;

            string path = "";
            //if (!FrameworkResolver.TryGetAssembly(name, targetFramework, out path))
            //{
            //    return null;
            //}

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
                        Type = LibraryTypes.FrameworkOrGacAssembly,
                    },
                    Path = path,
                    Dependencies = Enumerable.Empty<LibraryDependency>()
                };

                return library;
            }

            return null;
        }
    }
}
