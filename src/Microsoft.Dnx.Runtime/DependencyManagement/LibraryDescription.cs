// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Represents the result of resolving the library
    /// </summary>
    public class LibraryDescription
    {
        public LibraryDescription(
            LibraryRange requestedRange,
            LibraryIdentity identity,
            string path,
            string type, 
            IEnumerable<LibraryDependency> dependencies, 
            IEnumerable<string> assemblies,
            FrameworkName framework)
        {
            Path = path;
            RequestedRange = requestedRange;
            Identity = identity;
            Type = type;
            Dependencies = dependencies ?? Enumerable.Empty<LibraryDependency>();
            Assemblies = assemblies ?? Enumerable.Empty<string>();
            Framework = framework;
        }

        public LibraryRange RequestedRange { get; }
        public LibraryIdentity Identity { get; }

        public string Type { get; }
        public FrameworkName Framework { get; set; }

        public string Path { get; set; }
        public bool Resolved { get; set; } = true;
        public bool Compatible { get; set; } = true;
        public IEnumerable<string> Assemblies { get; set; }
        public IEnumerable<LibraryDependency> Dependencies { get; set; }

        internal Library ToLibrary()
        {
            return new Library(
                Identity.Name,
                Identity.Version?.GetNormalizedVersionString(),
                Path,
                Type,
                Dependencies.Select(d => d.Name),
                Assemblies.Select(a => new AssemblyName(a)));
        }
    }
}
