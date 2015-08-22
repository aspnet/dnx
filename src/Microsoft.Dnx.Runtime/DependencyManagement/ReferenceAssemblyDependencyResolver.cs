// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class ReferenceAssemblyDependencyResolver
    {
        public ReferenceAssemblyDependencyResolver(FrameworkReferenceResolver frameworkReferenceResolver)
        {
            FrameworkResolver = frameworkReferenceResolver;
        }

        private FrameworkReferenceResolver FrameworkResolver { get; set; }

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
                return new LibraryDescription(
                    libraryRange,
                    new LibraryIdentity(libraryRange.Name, new SemanticVersion(assemblyVersion), isGacOrFrameworkReference: true),
                    path,
                    LibraryTypes.ReferenceAssembly,
                    Enumerable.Empty<LibraryDependency>(),
                    new[] { name },
                    framework: targetFramework);
            }

            return null;
        }
    }
}
