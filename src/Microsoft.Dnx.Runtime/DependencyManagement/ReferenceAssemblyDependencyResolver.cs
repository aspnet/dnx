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
    public class ReferenceAssemblyDependencyResolver : IDependencyProvider
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

            var name = libraryRange.GetReferenceAssemblyName();
            var version = libraryRange.VersionRange?.MinVersion;

            string path;
            Version assemblyVersion;

            if (!FrameworkResolver.TryGetAssembly(name, targetFramework, out path, out assemblyVersion))
            {
                return null;
            }

            if (version == null || version.Version == assemblyVersion)
            {
                _resolvedPaths[libraryRange.Name] = path;

                return new LibraryDescription(
                    libraryRange,
                    new LibraryIdentity(libraryRange.Name, new SemanticVersion(assemblyVersion), isGacOrFrameworkReference: true),
                    path,
                    LibraryTypes.ReferenceAssembly,
                    Enumerable.Empty<LibraryDependency>(),
                    new[] { name },
                    framework: null);
            }

            return null;
        }

        public void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework, string runtimeIdentifier)
        {
        }
    }
}
