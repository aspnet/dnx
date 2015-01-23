// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet
{
    public class PackageRepository
    {
        private readonly Dictionary<string, IEnumerable<PackageInfo>> _cache;
        private readonly IFileSystem _repositoryRoot;
        private readonly bool _checkPackageIdCase;

        public PackageRepository(string path, bool caseSensitivePackagesName)
            : this(new PhysicalFileSystem(path), caseSensitivePackagesName)
        {
        }

        public PackageRepository(IFileSystem root, bool caseSensitivePackagesName)
        {
            _repositoryRoot = root;
            _checkPackageIdCase = caseSensitivePackagesName;

            _cache = new Dictionary<string, IEnumerable<PackageInfo>>(
                caseSensitivePackagesName ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        }

        public IFileSystem RepositoryRoot
        {
            get
            {
                return _repositoryRoot;
            }
        }

        public IDictionary<string, IEnumerable<PackageInfo>> GetAllPackages()
        {
            foreach (var packageDir in _repositoryRoot.GetDirectories("."))
            {
                var packageId = Path.GetFileName(packageDir);

                // This call add the package to the cache
                FindPackagesById(packageId);
            }

            return _cache;
        }

        public IEnumerable<PackageInfo> FindPackagesById(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException("packageId");
            }

            // packages\{packageId}\{version}\{packageId}.nuspec
            return _cache.GetOrAdd(packageId, id =>
            {
                var packages = new List<PackageInfo>();

                foreach (var versionDir in _repositoryRoot.GetDirectories(id))
                {
                    // versionDir = {packageId}\{version}
                    var folders = versionDir.Split(new[] { Path.DirectorySeparatorChar }, 2);

                    // Unknown format
                    if (folders.Length < 2)
                    {
                        continue;
                    }

                    var versionPart = folders[1];

                    // Get the version part and parse it
                    SemanticVersion version;
                    if (!SemanticVersion.TryParse(versionPart, out version))
                    {
                        continue;
                    }

                    // If we need to help ensure case-sensitivity, we try to get
                    // the package id in accurate casing by extracting the name of nuspec file
                    // Otherwise we just use the passed in package id for efficiency
                    if (_checkPackageIdCase)
                    {
                        var manifestFileName = Path.GetFileName(
                            _repositoryRoot.GetFiles(versionDir, "*" + Constants.ManifestExtension).FirstOrDefault());
                        if (string.IsNullOrEmpty(manifestFileName))
                        {
                            continue;
                        }
                        id = Path.GetFileNameWithoutExtension(manifestFileName);
                    }

                    packages.Add(new PackageInfo(_repositoryRoot, id, version, versionDir));
                }

                return packages;
            });
        }

        public void RemovePackage(PackageInfo package)
        {
            string packageName = package.Id;
            string packageVersion = package.Version.ToString();

            string folderToDelete;
            if (RepositoryRoot.GetDirectories(packageName).Count() >1)
            {
                // There is more than one version of this package so we can only
                // remove the version folder without risking to break something else
                folderToDelete = Path.Combine(packageName, packageVersion);
            }
            else
            {
                folderToDelete = packageName;
            }

            RepositoryRoot.DeleteDirectory(folderToDelete, recursive: true);
        }
    }
}