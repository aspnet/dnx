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
    public class ReferenceAssemblyDependencyProvider : IDependencyProvider 
    {
        private readonly Dictionary<string, string> _resolvedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ReferenceAssemblyDependencyProvider(IFrameworkReferenceResolver frameworkReferenceResolver)
        {
            FrameworkResolver = frameworkReferenceResolver;
        }

        private IFrameworkReferenceResolver FrameworkResolver { get; set; }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            Logger.TraceInformation($"[ReferenceAssemblyDependencyResolver] Resolving {libraryRange.Name} for {targetFramework}");
            System.Diagnostics.Debug.Assert(SupportsType(libraryRange.TypeConstraint));

            var name = libraryRange.Name;
            var version = libraryRange.VersionRange?.MinVersion;

            string path;
            if (!FrameworkResolver.TryGetAssembly(name, targetFramework, out path))
            {
                Logger.TraceWarning($"[ReferenceAssemblyDependencyResolver] Unable to resolve {libraryRange.Name}");
                return null;
            }

            var assemblyVersion = AssemblyUtils.GetAssemblyVersion(path);

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
                        Type = LibraryTypes.Reference
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
                LibraryTypes.Reference,
                StringComparison.Ordinal);
        }
    }
}
