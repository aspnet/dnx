// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet;

namespace Microsoft.Framework.Runtime.DependencyManagement
{
    public class LockFileLibrary
    {
        public string Name { get; set; }

        public SemanticVersion Version { get; set; }

        public IList<PackageDependencySet> DependencySets { get; set; } = new List<PackageDependencySet>();

        public IList<FrameworkAssemblyReference> FrameworkAssemblies { get; set; } = new List<FrameworkAssemblyReference>();

        public IList<PackageReferenceSet> PackageAssemblyReferences { get; set; } = new List<PackageReferenceSet>();

        public IList<IPackageFile> Files { get; set; } = new List<IPackageFile>();
    }
}
