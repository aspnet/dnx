// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Framework.Runtime;
using System.Security.Cryptography;
using NuGet;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackPackage
    {
        private readonly NuGetDependencyResolver _nugetDependencyResolver;
        private readonly LibraryDescription _libraryDescription;

        public PackPackage(NuGetDependencyResolver nugetDependencyResolver, LibraryDescription libraryDescription)
        {
            _nugetDependencyResolver = nugetDependencyResolver;
            _libraryDescription = libraryDescription;
        }

        public Library Library { get { return _libraryDescription.Identity; } }

        public string TargetPath { get; private set; }

        public void Emit(PackRoot root)
        {
            var package = _nugetDependencyResolver.FindCandidate(
                _libraryDescription.Identity.Name,
                _libraryDescription.Identity.Version,
                _libraryDescription.Identity.Configuration);

            Console.WriteLine("Packing nupkg dependency {0} {1}", package.Id, package.Version);

            var resolver = new DefaultPackagePathResolver(root.PackagesPath);

            TargetPath = resolver.GetInstallPath(package.Id, package.Version, root.Configuration);

            if (Directory.Exists(TargetPath))
            {
                if (root.Overwrite)
                {
                    root.Operations.Delete(TargetPath);
                }
                else
                {
                    Console.WriteLine("  {0} already exists.", TargetPath);
                    return;
                }
            }

            Console.WriteLine("  Target {0}", TargetPath);

            var targetNupkgPath = resolver.GetPackageFilePath(package.Id, package.Version, root.Configuration);
            var hashPath = resolver.GetHashPath(package.Id, package.Version, root.Configuration);

            using (var sourceStream = package.GetStream())
            {
                using (var archive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                {
                    root.Operations.ExtractNupkg(archive, TargetPath);
                }
            }
            using (var sourceStream = package.GetStream())
            {
                using (var targetStream = new FileStream(targetNupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sourceStream.CopyTo(targetStream);
                }

                sourceStream.Seek(0, SeekOrigin.Begin);
                var sha512Bytes = SHA512.Create().ComputeHash(sourceStream);
                File.WriteAllText(hashPath, Convert.ToBase64String(sha512Bytes));
            }
        }
    }
}
