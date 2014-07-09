// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
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
                throw new ArgumentNullException("fileSystem");
            }
            _fileSystem = fileSystem;
        }

        public virtual string GetInstallPath(IPackage package)
        {
            return Path.Combine(_fileSystem.Root, GetPackageDirectory(package));
        }

        public virtual string GetPackageDirectory(IPackage package)
        {
            return GetPackageDirectory(package.Id, package.Version);
        }

        public virtual string GetPackageFileName(IPackage package)
        {
            return GetPackageFileName(package.Id, package.Version);
        }

        public virtual string GetPackageDirectory(string packageId, SemanticVersion version)
        {
            string directory = packageId;
            if (_useSideBySidePaths)
            {
                directory = Path.Combine(directory, version.ToString());
            }
            return directory;
        }

        public virtual string GetPackageFileName(string packageId, SemanticVersion version)
        {
            string fileNameBase = packageId;
            if (_useSideBySidePaths)
            {
                fileNameBase = Path.Combine(fileNameBase, version.ToString());
            }
            return fileNameBase + Constants.PackageExtension;
        }
    }
}
