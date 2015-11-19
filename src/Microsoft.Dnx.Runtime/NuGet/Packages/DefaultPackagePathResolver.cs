// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet
{
    public class DefaultPackagePathResolver : IPackagePathResolver
    {
        private readonly string _path;

        public DefaultPackagePathResolver(string path)
        {
            _path = path;
        }

        public virtual string GetInstallPath(string packageId, SemanticVersion version)
        {
            return Path.Combine(_path, GetPackageDirectory(packageId, version));
        }

        public string GetPackageFilePath(string packageId, SemanticVersion version)
        {
            return Path.Combine(GetInstallPath(packageId, version),
                                GetPackageFileName(packageId, version));
        }

        public string GetManifestFilePath(string packageId, SemanticVersion version)
        {
            return Path.Combine(GetInstallPath(packageId, version),
                                GetManifestFileName(packageId, version));
        }

        public string GetHashPath(string packageId, SemanticVersion version)
        {
            return Path.Combine(GetInstallPath(packageId, version),
                                $"{packageId}.{version}{Constants.HashFileExtension}");
        }

        public virtual string GetPackageDirectory(string packageId, SemanticVersion version)
        {
            return Path.Combine(packageId, version.ToString());
        }

        public virtual string GetPackageFileName(string packageId, SemanticVersion version)
        {
            return $"{packageId}.{version}{Constants.PackageExtension}";
        }

        public virtual string GetManifestFileName(string packageId, SemanticVersion version)
        {
            return packageId + Constants.ManifestExtension;
        }
    }
}
