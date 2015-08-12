// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryDescription
    {
        public LibraryRange LibraryRange { get; set; }
        public LibraryIdentity Identity { get; set; }
        public IEnumerable<LibraryDependency> Dependencies { get; set; }

        public bool Resolved { get; set; } = true;
        public CompatibilityIssue CompatibilityIssue { get; set; }
        public bool Compatible
        {
            get
            {
                return CompatibilityIssue == null;
            }
        }

        public string Path { get; set; }
        public string Type { get; set; }
        public FrameworkName Framework { get; set; }
        public IEnumerable<string> LoadableAssemblies { get; set; }

        public Library ToLibrary()
        {
            return new Library(
                Identity.Name,
                Identity.Version?.GetNormalizedVersionString(),
                Path,
                Type,
                Dependencies.Select(d => d.Name),
                LoadableAssemblies.Select(a => new AssemblyName(a)));
        }
    }
}
