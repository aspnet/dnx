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
        private readonly Dictionary<string, IEnumerable<PackageInfo>> _cache = new Dictionary<string, IEnumerable<PackageInfo>>(StringComparer.OrdinalIgnoreCase);
        private readonly IFileSystem _repositoryRoot;
        private readonly bool _checkPackageIdCase;

        public PackageRepository(string path, bool checkPackageIdCase)
        {
            _repositoryRoot = new PhysicalFileSystem(path);
            _checkPackageIdCase = checkPackageIdCase;
        }

        public IFileSystem RepositoryRoot
        {
            get
            {
                return _repositoryRoot;
            }
        }

        public IEnumerable<PackageInfo> FindPackagesById(string packageId)
        {
            if (String.IsNullOrEmpty(packageId))
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
                            throw new Exception(
                                string.Format("TODO: Manifest file is missing from {0}",
                                _repositoryRoot.GetFullPath(versionDir)));
                        }
                        id = Path.GetFileNameWithoutExtension(manifestFileName);
                    }

                    packages.Add(new PackageInfo(_repositoryRoot, id, version, versionDir));
                }

                return packages;
            });
        }
    }
}