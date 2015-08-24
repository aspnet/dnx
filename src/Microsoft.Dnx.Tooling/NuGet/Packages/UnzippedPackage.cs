// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;
using NuGet.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;

namespace NuGet
{
    /// <summary>
    /// Summary description for UnzippedPackage
    /// </summary>
    public class UnzippedPackage : LocalPackage
    {
        private Dictionary<string, PhysicalPackageFile> _files;
        private readonly IFileSystem _fileSystem;
        private readonly string _manifestPath;

        public UnzippedPackage(IFileSystem fileSystem, string manifestPath)
        {
            if (fileSystem == null)
            {
                throw new ArgumentNullException(nameof(fileSystem));
            }

            if (String.IsNullOrEmpty(manifestPath))
            {
                throw new ArgumentNullException(nameof(manifestPath));
            }

            string manifestFullPath = fileSystem.GetFullPath(manifestPath);
            string directory = Path.GetDirectoryName(manifestFullPath);
            _fileSystem = new PhysicalFileSystem(directory);
            _manifestPath = Path.GetFileName(manifestFullPath);

            EnsureManifest();
        }

        private void EnsureManifest()
        {
            using (Stream stream = _fileSystem.OpenFile(_manifestPath))
            {
                ReadManifest(stream);
            }
        }

        protected override IEnumerable<IPackageFile> GetFilesBase()
        {
            EnsurePackageFiles();
            return _files.Values;
        }

        public override Stream GetStream()
        {
            var nupkgName = Id + "." + Version + Constants.PackageExtension;
            if (_fileSystem.FileExists(nupkgName))
            {
                return _fileSystem.OpenFile(nupkgName);
            }
            else if (_fileSystem.FileExists(Path.Combine("..", nupkgName)))
            {
                return _fileSystem.OpenFile(Path.Combine("..", nupkgName));
            }
            else
            {
                throw new Exception("nupkg file missing from source");
            }
        }

        protected override IEnumerable<IPackageAssemblyReference> GetAssemblyReferencesCore()
        {
            EnsurePackageFiles();

            return from file in _files.Values
                   where IsAssemblyReference(file.Path)
                   select (IPackageAssemblyReference)new PhysicalPackageAssemblyReference(file);
        }

        protected override IEnumerable<IPackageAssemblyReference> GetResourceReferencesCore()
        {
            EnsurePackageFiles();

            return from file in _files.Values
                   where IsResourcesReference(file.Path)
                   select(IPackageAssemblyReference)new PhysicalPackageAssemblyReference(file);
        }

        private void EnsurePackageFiles()
        {
            if (_files != null)
            {
                return;
            }

            _files = new Dictionary<string, PhysicalPackageFile>();
            foreach (var filePath in _fileSystem.GetFiles("", "*.*", true))
            {
                _files[filePath] = new PhysicalPackageFile
                {
                    SourcePath = _fileSystem.GetFullPath(filePath),
                    TargetPath = filePath
                };
            }
        }
    }
}