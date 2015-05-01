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
        private readonly FrameworkName _targetFramework;
        private readonly ReadOnlyCollection<PackageDependency> _dependencies;

        public PackageDependencySet(FrameworkName targetFramework, IEnumerable<PackageDependency> dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException("dependencies");
            }

            _targetFramework = targetFramework;
            _dependencies = new ReadOnlyCollection<PackageDependency>(dependencies.ToList());
        }

        public FrameworkName TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        public ICollection<PackageDependency> Dependencies
        {
            get
            {
                return _dependencies;
            }
        }

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