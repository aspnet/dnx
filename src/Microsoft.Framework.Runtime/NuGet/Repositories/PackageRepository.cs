// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet
{
    public class PackageRepository
    {
        private readonly ILookup<string, IPackage> _cache;

        public PackageRepository(string physicalPath)
            : this(new DefaultPackagePathResolver(physicalPath),
                   new PhysicalFileSystem(physicalPath))
        {
        }

        public PackageRepository(IPackagePathResolver pathResolver, IFileSystem fileSystem)
        {
            if (pathResolver == null)
            {
                throw new ArgumentNullException("pathResolver");
            }

            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }

            FileSystem = fileSystem;
            PathResolver = pathResolver;
            _cache = PopulateCache();
        }

        private ILookup<string, IPackage> PopulateCache()
        {
            string nuspecFilter = "*" + Constants.ManifestExtension;

            var packages = new List<IPackage>();

            // packages\{name}\{version}\{name}.nuspec

            foreach (var pakageDir in FileSystem.GetDirectories(String.Empty))
            {
                foreach (var versionDir in FileSystem.GetDirectories(pakageDir))
                {
                    foreach (var nuspecPath in FileSystem.GetFiles(versionDir, nuspecFilter))
                    {
                        packages.Add(new UnzippedPackage(FileSystem, nuspecPath));
                    }
                }
            }

            return packages.ToLookup(p => p.Id, StringComparer.OrdinalIgnoreCase);
        }

        public IPackagePathResolver PathResolver
        {
            get;
            set;
        }

        public IFileSystem FileSystem
        {
            get;
            private set;
        }

        public IEnumerable<IPackage> FindPackagesById(string packageId)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException("packageId");
            }

            return _cache[packageId];
        }
    }
}