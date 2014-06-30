// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Resources;

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

            foreach (var dir in FileSystem.GetDirectories(String.Empty))
            {
                foreach (var path in FileSystem.GetFiles(dir, nuspecFilter))
                {
                    packages.Add(OpenNuspec(path));
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


        private IPackage OpenNuspec(string path)
        {
            if (!FileSystem.FileExists(path))
            {
                return null;
            }

            if (Path.GetExtension(path) == Constants.ManifestExtension)
            {
                UnzippedPackage package;

                try
                {
                    package = new UnzippedPackage(FileSystem, path);
                }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, NuGetResources.ErrorReadingPackage, path), ex);
                }

                // Set the last modified date on the package
                package.Published = FileSystem.GetLastModified(path);

                return package;
            }

            return null;
        }

        private string GetPackageFilePath(IPackage package)
        {
            return Path.Combine(PathResolver.GetPackageDirectory(package),
                                PathResolver.GetPackageFileName(package));
        }

        private string GetPackageFilePath(string id, SemanticVersion version)
        {
            return Path.Combine(PathResolver.GetPackageDirectory(id, version),
                                PathResolver.GetPackageFileName(id, version));
        }
    }
}