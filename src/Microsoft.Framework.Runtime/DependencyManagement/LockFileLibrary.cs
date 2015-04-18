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

        public bool IsServiceable { get; set; }

        public string Sha512 { get; set; }

        public IList<string> Files { get; set; } = new List<string>();
    }

    public class LockFileTarget
    {
        public FrameworkName TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public IList<LockFileTargetLibrary> Libraries { get; set; } = new List<LockFileTargetLibrary>();
    }

    public class LockFileTargetLibrary
    {
        public string Name { get; set; }

        public SemanticVersion Version { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public IList<FrameworkAssemblyReference> FrameworkAssemblies { get; set; } = new List<FrameworkAssemblyReference>();

        public IList<string> RuntimeAssemblies { get; set; } = new List<string>();

        public IList<string> CompileTimeAssemblies { get; set; } = new List<string>();

        public IList<string> NativeLibraries { get; set; } = new List<string>();
    }
}
