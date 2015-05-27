// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet
{
    public class PackageDependencySet : IFrameworkTargetable
    {
        public PackageDependencySet(IEnumerable<PackageDependency> dependencies)
            : this((FrameworkName)null, dependencies)
        {
        }

        public PackageDependencySet(string targetFramework, IEnumerable<PackageDependency> dependencies)
            : this(targetFramework != null ? VersionUtility.ParseFrameworkName(targetFramework) : null, dependencies)
        {
        }

        public PackageDependencySet(FrameworkName targetFramework, IEnumerable<PackageDependency> dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            TargetFramework = targetFramework;
            Dependencies = new ReadOnlyCollection<PackageDependency>(dependencies.ToList());
        }

        public FrameworkName TargetFramework { get; }

        public ICollection<PackageDependency> Dependencies { get; }

        public IEnumerable<FrameworkName> SupportedFrameworks
        {
            get
            {
                if (TargetFramework == null)
                {
                    yield break;
                }

                yield return TargetFramework;
            }
        }
    }
}