// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Compilation;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class ReferenceAssemblyDependencyResolver : IDependencyProvider, ILibraryExportProvider
    {
        private readonly Dictionary<string, string> _resolvedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ReferenceAssemblyDependencyResolver(FrameworkReferenceResolver frameworkReferenceResolver)
        {
            FrameworkResolver = frameworkReferenceResolver;
        }

        private FrameworkReferenceResolver FrameworkResolver { get; set; }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            string path = FrameworkResolver.GetFrameworkPath(targetFramework);
            if (!string.IsNullOrEmpty(path))
            {
                return new[]
                {
                    Path.Combine(path, "{name}.dll"),
                    Path.Combine(path, "Facades", "{name}.dll")
                };
            }

            return Enumerable.Empty<string>();
        }

        public LibraryDescription GetDescription(LibraryRange libraryRange, FrameworkName targetFramework)
        {
            if (!libraryRange.IsGacOrFrameworkReference)
            {
                return null;
            }

            var name = libraryRange.Name;
            var version = libraryRange.VersionRange?.MinVersion;

            string path;
            Version assemblyVersion;

            if (!FrameworkResolver.TryGetAssembly(name, targetFramework, out path, out assemblyVersion))
            {
                return null;
            }

            if (version == null || version.Version == assemblyVersion)
            {
                _resolvedPaths[name] = path;

                return new LibraryDescription
                {
                    LibraryRange = libraryRange,
                    Identity = new Library
                    {
                        Name = name,
                        Version = new SemanticVersion(assemblyVersion),
                        IsGacOrFrameworkReference = true
                    },
                    LoadableAssemblies = new[] { name },
                    Dependencies = Enumerable.Empty<LibraryDependency>()
                };
            }

            return null;
        }

        public void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework, string runtimeIdentifier)
        {
            foreach (var d in dependencies)
            {
                d.Path = _resolvedPaths[d.Identity.Name];
                d.Type = "Assembly";
            }
        }

        public ILibraryExport GetLibraryExport(ILibraryKey target)
        {
            // Did we even resolve this name, if not then do nothing
            if (!_resolvedPaths.ContainsKey(target.Name))
            {
                return null;
            }

            // We can't use resolved paths since it might be different to the target framework
            // being passed in here. After we know this resolver is handling the
            // requested name, we can call back into the FrameworkResolver to figure out
            //  the specific path for the target framework

            string path;
            Version version;

            if (FrameworkResolver.TryGetAssembly(target.Name, target.TargetFramework, out path, out version))
            {
                return new LibraryExport(new MetadataFileReference(target.Name, path));
            }

            return null;
        }
    }
}