// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackPackage
    {
        private readonly LibraryDescription _libraryDescription;

        public PackPackage(LibraryDescription libraryDescription)
        {
            _libraryDescription = libraryDescription;
        }

        public Library Library { get { return _libraryDescription.Identity; } }

        public string TargetPath { get; private set; }

        public void Emit(PackRoot root)
        {
            foreach (var context in root.LibraryDependencyContexts[Library])
            {
                root.Reports.Quiet.WriteLine("Using {0} dependency {1} for {2}",
                    _libraryDescription.Type, Library, context.FrameworkName);
                Emit(root, context.PackageAssemblies[Library.Name]);
            }
        }

        private void Emit(PackRoot root, IEnumerable<PackageAssembly> assemblies)
        {
            var resolver = new DefaultPackagePathResolver(root.TargetPackagesPath);

            TargetPath = resolver.GetInstallPath(Library.Name, Library.Version);

            root.Reports.Quiet.WriteLine("  Source: {0}", _libraryDescription.Path);
            root.Reports.Quiet.WriteLine("  Target: {0}", TargetPath);

            Directory.CreateDirectory(TargetPath);

            // Copy nuspec
            var nuspecName = resolver.GetManifestFileName(Library.Name, Library.Version);
            CopyFile(root, Path.Combine(_libraryDescription.Path, nuspecName), Path.Combine(TargetPath, nuspecName), root.Overwrite);

            // Copy assemblies for current framework
            foreach (var assembly in assemblies)
            {
                root.Reports.Quiet.WriteLine("  File: {0}", assembly.RelativePath);

                var targetAssemblyPath = Path.Combine(TargetPath, assembly.RelativePath);
                CopyFile(root, assembly.Path, targetAssemblyPath, root.Overwrite);
            }

            // Special cases
            var specialFolders = new[] { "native", "InteropAssemblies", "redist", Path.Combine("lib", "contract") };
            foreach (var folder in specialFolders)
            {
                var srcFolder = Path.Combine(_libraryDescription.Path, folder);
                var targetFolder = Path.Combine(TargetPath, folder);
                CopyFolder(root, srcFolder, targetFolder);
            }
        }

        private void CopyFolder(PackRoot root, string srcFolder, string targetFolder)
        {
            if (!Directory.Exists(srcFolder))
            {
                return;
            }

            if (Directory.Exists(targetFolder))
            {
                if (root.Overwrite)
                {
                    root.Operations.Delete(targetFolder);
                }
                else
                {
                    root.Reports.Quiet.WriteLine("  {0} already exists", targetFolder);
                    return;
                }
            }

            Directory.CreateDirectory(targetFolder);
            root.Operations.Copy(srcFolder, targetFolder);
        }

        private void CopyFile(PackRoot root, string srcPath, string targetPath, bool overwrite)
        {
            var targetFolder = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(targetFolder);

            if (File.Exists(targetPath))
            {
                if (overwrite)
                {
                    File.Delete(targetPath);
                }
                else
                {
                    root.Reports.Quiet.WriteLine("  {0} already exists", targetPath);
                    return;
                }
            }

            File.Copy(srcPath, targetPath);
        }
    }
}
