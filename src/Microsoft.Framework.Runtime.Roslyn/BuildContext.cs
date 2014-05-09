// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class BuildContext
    {
        public BuildContext(string assemblyName, FrameworkName targetFramework)
        {
            AssemblyName = assemblyName;
            TargetFramework = targetFramework;
        }

        public string AssemblyName { get; private set; }

        public FrameworkName TargetFramework { get; private set; }

        public string OutputPath { get; set; }

        public PackageBuilder PackageBuilder { get; set; }

        public PackageBuilder SymbolPackageBuilder { get; set; }

        public CompilationContext CompilationContext { get; set; }
    }
}
