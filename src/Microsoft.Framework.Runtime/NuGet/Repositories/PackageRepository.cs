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
        private readonly bool _isInRuntimeContext;

        public PackageRepository(string path, bool isInRuntimeContext)
        {
            _repositoryRoot = new PhysicalFileSystem(path);
            _isInRuntimeContext = isInRuntimeContext;
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

                    // If this object is not used by runtime, it must be used by "kpm restore"
                    // We need to help "kpm restore" to ensure case-sensitivity here. So we try to
                    // get the package id in accurate casing by extracting the name of nuspec file
                    if (!_isInRuntimeContext)
                    {
                        // Get the accurate package id to ensure case-sensitivity
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

                    // Runtime already ensures case-sensitivity. So if this object is used by runtime,
                    // we don't need to iterate files to get the package id in accurate casing and
                    // we use the passed in package id for efficiency
                    packages.Add(new PackageInfo(_repositoryRoot, id, version, versionDir));
                }

                return packages;
            });
        }
    }
}