// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.Framework.Runtime.Internal
{
    public class PackagePathResolver
    {
        private readonly string _path;
        private readonly IEnumerable<string> _cachePaths;

        public PackagePathResolver(string path, IEnumerable<string> cachePaths)
        {
            _path = path;
            _cachePaths = cachePaths;
        }

        public string ResolvePackagePath(string expectedHash, string name, NuGetVersion version)
        {
            foreach (var cachePath in _cachePaths)
            {
                var cacheHashFile = GetHashPath(cachePath, name, version);

                // REVIEW: More efficient compare?
                if (File.Exists(cacheHashFile) &&
                    File.ReadAllText(cacheHashFile) == expectedHash)
                {
                    return GetInstallPath(cachePath, name, version);
                }
            }
            return GetInstallPath(_path, name, version);
        }
        
        private string GetInstallPath(string root, string packageId, NuGetVersion version)
        {
            return Path.Combine(root, GetPackageDirectory(packageId, version));
        }

        private string GetHashPath(string root, string packageId, NuGetVersion version)
        {
            return Path.Combine(GetInstallPath(root, packageId, version),
                                string.Format("{0}.{1}.nupkg.sha512", packageId, version));
        }

        private string GetPackageDirectory(string packageId, NuGetVersion version)
        {
            return Path.Combine(packageId, version.ToString());
        }
    }
}
