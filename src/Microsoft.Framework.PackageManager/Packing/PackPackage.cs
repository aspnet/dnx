// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
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
                Console.WriteLine("Packing nupkg dependency {0} for {1}", Library, context.FrameworkName);
                Emit(root, context.PackageAssemblies[Library.Name]);
            }
        }

        private void Emit(PackRoot root, IEnumerable<PackageAssembly> assemblies)
        {
            var resolver = new DefaultPackagePathResolver(root.TargetPackagesPath);

            TargetPath = resolver.GetInstallPath(Library.Name, Library.Version);

            Directory.CreateDirectory(TargetPath);

            // Copy nuspec
            var nuspecName = resolver.GetManifestFileName(Library.Name, Library.Version);
            CopyFile(Path.Combine(_libraryDescription.Path, nuspecName), Path.Combine(TargetPath, nuspecName), root.Overwrite);

            // Copy assemblies for current framework
            foreach (var assembly in assemblies)
            {
                var targetAssemblyPath = Path.Combine(TargetPath, assembly.RelativePath);
                CopyFile(assembly.Path, targetAssemblyPath, root.Overwrite);
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

        private static void CopyFolder(PackRoot root, string srcFolder, string targetFolder)
        {
            if (!Directory.Exists(srcFolder))
            {
                return;
            }

            if (Directory.Exists(targetFolder))
            {
                if (root.Overwrite)
                {
                    Directory.Delete(targetFolder, recursive: true);
                }
                else
                {
                    Console.WriteLine("  {0} already exists", targetFolder);
                    return;
                }
            }

            Console.WriteLine("  Target {0}", targetFolder);
            Directory.CreateDirectory(targetFolder);
            root.Operations.Copy(srcFolder, targetFolder);
        }

        private static void CopyFile(string srcPath, string targetPath, bool overwrite)
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
                    Console.WriteLine("  {0} already exists", targetPath);
                    return;
                }
            }

            Console.WriteLine("  Target {0}", targetPath);
            File.Copy(srcPath, targetPath);
        }
    }
}
