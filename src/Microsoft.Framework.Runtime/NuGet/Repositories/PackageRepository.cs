// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet
{
    public class PackageRepository
    {
        private readonly Dictionary<string, IEnumerable<IPackage>> _cache = new Dictionary<string, IEnumerable<IPackage>>(StringComparer.OrdinalIgnoreCase);
        private readonly IFileSystem _repositoryRoot;

        public PackageRepository(string path)
        {
            _repositoryRoot = new PhysicalFileSystem(path);
        }

        public IFileSystem RepositoryRoot
        {
            get
            {
                return _repositoryRoot;
            }
        }

        public IEnumerable<IPackage> FindPackagesById(string packageId)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException("packageId");
            }

            // packages\{name}\{version}\{name}.nuspec
            return _cache.GetOrAdd(packageId, id =>
            {
                var packages = new List<IPackage>();

                foreach (var versionDir in _repositoryRoot.GetDirectories(id))
                {
                    foreach (var nuspecPath in _repositoryRoot.GetFiles(versionDir, "*.nuspec"))
                    {
                        packages.Add(new UnzippedPackage(_repositoryRoot, nuspecPath));
                    }
                }

                return packages;
            });
        }
    }
}