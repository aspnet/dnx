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

namespace Microsoft.Framework.Runtime.Hosting.DependencyProviders
{
    public class ReferenceAssemblyDependencyResolver : IDependencyProvider 
    {
        private readonly Dictionary<string, string> _resolvedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ReferenceAssemblyDependencyResolver(IFrameworkReferenceResolver frameworkReferenceResolver)
        {
            FrameworkResolver = frameworkReferenceResolver;
        }

        private IFrameworkReferenceResolver FrameworkResolver { get; set; }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            Logger.TraceInformation($"[ReferenceAssemblyDependencyResolver] Resolving {libraryRange.Name} for {targetFramework}");
            if (!SupportsType(libraryRange.Type))
            {
                return null;
            }

            var name = libraryRange.Name;
            var version = libraryRange.VersionRange?.MinVersion;

            string path;
            if (!FrameworkResolver.TryGetAssembly(name, targetFramework, out path))
            {
                Logger.TraceWarning($"[ReferenceAssemblyDependencyResolver] Unable to resolve {libraryRange.Name}");
                return null;
            }

            var assemblyVersion = GetAssemblyVersion(path);

            if (version == null || version == assemblyVersion)
            {
                _resolvedPaths[name] = path;

                Logger.TraceInformation($"[ReferenceAssemblyDependencyResolver] Resolved {libraryRange.Name} to {path}");

                return new Library
                {
                    LibraryRange = libraryRange,
                    Identity = new LibraryIdentity
                    {
                        Name = name,
                        Version = assemblyVersion,
                        Type = LibraryTypes.ReferenceAssembly
                    },
                    Dependencies = Enumerable.Empty<LibraryDependency>()
                };
            }

            return null;
        }

        public bool SupportsType(string libraryType)
        {
            return string.Equals(
                libraryType,
                LibraryTypes.ReferenceAssembly,
                StringComparison.Ordinal);
        }

        internal static NuGetVersion GetAssemblyVersion(string path)
        {
#if ASPNET50
            return new NuGetVersion(AssemblyName.GetAssemblyName(path).Version);
#else
            return new NuGetVersion(System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path).Version);
#endif
        }
    }
}
