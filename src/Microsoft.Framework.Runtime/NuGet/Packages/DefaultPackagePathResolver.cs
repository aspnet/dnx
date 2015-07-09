// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet
{
    public class DefaultPackagePathResolver : IPackagePathResolver
    {
        private readonly IFileSystem _fileSystem;
        private readonly bool _useSideBySidePaths;

        public DefaultPackagePathResolver(string path)
            : this(new PhysicalFileSystem(path))
        {
        }

        public DefaultPackagePathResolver(IFileSystem fileSystem)
            : this(fileSystem, useSideBySidePaths: true)
        {
        }

        public DefaultPackagePathResolver(string path, bool useSideBySidePaths)
            : this(new PhysicalFileSystem(path), useSideBySidePaths)
        {
        }

        public DefaultPackagePathResolver(IFileSystem fileSystem, bool useSideBySidePaths)
        {
            _useSideBySidePaths = useSideBySidePaths;
            if (fileSystem == null)
            {
                throw new ArgumentNullException(nameof(fileSystem));
            }
            _fileSystem = fileSystem;
        }

        public virtual string GetInstallPath(string packageId, SemanticVersion version)
        {
            return Path.Combine(_fileSystem.Root, GetPackageDirectory(packageId, version));
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
                                $"{packageId}.{version.GetNormalizedVersionString()}{Constants.HashFileExtension}");
        }

        public virtual string GetPackageDirectory(string packageId, SemanticVersion version)
        {
            string directory = packageId;
            if (_useSideBySidePaths)
            {
                directory = Path.Combine(directory, version.GetNormalizedVersionString());
            }
            return directory;
        }

        public virtual string GetPackageFileName(string packageId, SemanticVersion version)
        {
            string fileNameBase = packageId;
            if (_useSideBySidePaths)
            {
                fileNameBase += "." + version.GetNormalizedVersionString();
            }
            return fileNameBase + Constants.PackageExtension;
        }

        public virtual string GetManifestFileName(string packageId, SemanticVersion version)
        {
            return packageId + Constants.ManifestExtension;
        }
    }
}
