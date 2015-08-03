// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;

namespace NuGet
{
    public class PackageRepository
    {
        private readonly Dictionary<string, IEnumerable<PackageInfo>> _cache;
        private readonly IFileSystem _repositoryRoot;
        private readonly bool _checkPackageIdCase;
        private ILookup<string, LockFilePackageLibrary> _lockFileLibraries;

        public PackageRepository(string path, bool caseSensitivePackagesName = false)
            : this(new PhysicalFileSystem(path), caseSensitivePackagesName)
        {
        }

        public PackageRepository(IFileSystem root, bool caseSensitivePackagesName = false)
        {
            _repositoryRoot = root;
            _checkPackageIdCase = caseSensitivePackagesName;

            _cache = new Dictionary<string, IEnumerable<PackageInfo>>(
                caseSensitivePackagesName ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        }

        public bool CheckHashFile { get; set; }

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

        public void ApplyLockFile(LockFile lockFile)
        {
            var stringComparer = _checkPackageIdCase ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            _lockFileLibraries = lockFile.PackageLibraries
                                         .ToLookup(l => l.Name, stringComparer);
        }

        public IEnumerable<PackageInfo> FindPackagesById(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            // packages\{packageId}\{version}\{packageId}.nuspec
            return _cache.GetOrAdd(packageId, id =>
            {
                var packages = new List<PackageInfo>();

                if (_lockFileLibraries != null)
                {
                    foreach (var lockFileLibrary in _lockFileLibraries[packageId])
                    {
                        packages.Add(new PackageInfo(
                            _repositoryRoot,
                            lockFileLibrary.Name,
                            lockFileLibrary.Version,
                            Path.Combine(
                                lockFileLibrary.Name,
                                lockFileLibrary.Version.ToString()),
                            lockFileLibrary));
                    }

                    return packages;
                }

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

                    if (!version.IsOriginalStringNormalized())
                    {
                        // For a non-http match, if the OriginalVersion string is not normalized that means name of the folder which contains
                        // the package is not a normalized string. It will cause trouble for file searching in later stage. By invalidating this 
                        // match, it ensures the package will be reinstalled under a correct folder. This change ensures a package installed 
                        // by older version of DNX won't prevent new DNX to install correct package.
                        continue;
                    }

                    var manifestFilePath = _repositoryRoot.GetFiles(versionDir, "*" + Constants.ManifestExtension)
                        .FirstOrDefault();
                    if (string.IsNullOrEmpty(manifestFilePath))
                    {
                        // This is a corrupted packages because {id}.nupsec is missing
                        continue;
                    }

                    if (CheckHashFile && !_repositoryRoot.GetFiles(versionDir, "*" + Constants.HashFileExtension).Any())
                    {
                        // Writing the marker file is the last operation performed by NuGetPackageUtils.InstallFromStream. We'll use the
                        // presence of the file to denote the package was successfully installed.
                        continue;
                    }

                    // If we need to help ensure case-sensitivity, we try to get
                    // the package id in accurate casing by extracting the name of nuspec file
                    // Otherwise we just use the passed in package id for efficiency
                    if (_checkPackageIdCase)
                    {
                        id = Path.GetFileNameWithoutExtension(manifestFilePath);
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
            if (RepositoryRoot.GetDirectories(packageName).Count() > 1)
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
