// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime.DependencyManagement
{
    public class LockFileLibrary
    {
        public string Name { get; set; }

        public SemanticVersion Version { get; set; }

        public string Sha { get; set; }

        public IList<LockFileFrameworkGroup> FrameworkGroups { get; set; } = new List<LockFileFrameworkGroup>();

        public IList<string> Files { get; set; } = new List<string>();
    }

    public class LockFileFrameworkGroup
    {
        public FrameworkName TargetFramework { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public IList<FrameworkAssemblyReference> FrameworkAssemblies { get; set; } = new List<FrameworkAssemblyReference>();

        public IList<string> RuntimeAssemblies { get; set; } = new List<string>();

        public IList<string> CompileTimeAssemblies { get; set; } = new List<string>();
    }
}
