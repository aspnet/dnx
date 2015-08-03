// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public class UnresolvedDependencyProvider : IDependencyProvider
    {
        public LibraryDescription GetDescription(LibraryRange libraryRange, FrameworkName targetFramework)
        {
            return new LibraryDescription(
                libraryRange,
                new LibraryIdentity(libraryRange.Name, libraryRange.VersionRange?.MinVersion, libraryRange.IsGacOrFrameworkReference),
                path: null,
                type: LibraryTypes.Unresolved,
                dependencies: Enumerable.Empty<LibraryDependency>(),
                assemblies: Enumerable.Empty<string>(),
                framework: null)
            {
                Resolved = false
            };
        }

        public void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework, string runtimeIdentifier)
        {
        }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return Enumerable.Empty<string>();
        }
    }
}