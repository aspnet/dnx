// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Framework.PackageManager.Publish;
using Microsoft.Framework.Runtime;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Build;
using DefaultPackagePathResolver = NuGet.Packaging.DefaultPackagePathResolver;
using Library = Microsoft.Framework.Runtime.Library;

namespace Microsoft.Framework.PackageManager
{
    internal static class NuGetPackageUtils
    {
        internal static async Task InstallFromStream(
            Stream stream,
            LibraryIdentity library,
            string packagesDirectory,
            IReport information)
        {
            var packagePathResolver = new DefaultPackagePathResolver(packagesDirectory);

            var targetPath = packagePathResolver.GetInstallPath(library.Name, library.Version);
            var targetNuspec = packagePathResolver.GetManifestFilePath(library.Name, library.Version);
            var targetNupkg = packagePathResolver.GetPackageFilePath(library.Name, library.Version);
            var hashPath = packagePathResolver.GetHashPath(library.Name, library.Version);

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            await ConcurrencyUtilities.ExecuteWithFileLocked(targetNupkg, async createdNewLock =>
            {
                // If this is the first process trying to install the target nupkg, go ahead
                // After this process successfully installs the package, all other processes
                // waiting on this lock don't need to install it again.
                if (createdNewLock && !File.Exists(targetNupkg))
                {
                    information.WriteLine($"Installing {library.Name.Bold()} {library.Version}");

                    Directory.CreateDirectory(targetPath);
                    using (var nupkgStream = new FileStream(
                        targetNupkg,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        useAsync: true))
                    {
                        await stream.CopyToAsync(nupkgStream);
                        nupkgStream.Seek(0, SeekOrigin.Begin);

                        ExtractPackage(targetPath, nupkgStream);
                    }

                    // DNU REFACTORING TODO: bring this back after we have implementation of NuSpecFormatter.Read()
                    // Fixup the casing of the nuspec on disk to match what we expect
                    /*var nuspecFile = Directory.EnumerateFiles(targetPath, "*" + NuGet.Constants.ManifestExtension).Single();

                    if (!string.Equals(nuspecFile, targetNuspec, StringComparison.Ordinal))
                    {
                        MetadataBuilder metadataBuilder = null;
                        var nuspecFormatter = new NuSpecFormatter();
                        using (var nuspecStream = File.OpenRead(nuspecFile))
                        {
                            metadataBuilder = nuspecFormatter.Read(nuspecStream);
                            // REVIEW: any way better hardcoding "id"?
                            metadataBuilder.SetMetadataValue("id", library.Name);
                        }

                        // Delete the previous nuspec file
                        File.Delete(nuspecFile);

                        // Write the new manifest
                        using (var targetNuspecStream = File.OpenWrite(targetNuspec))
                        {
                            nuspecFormatter.Save(metadataBuilder, targetNuspecStream);
                        }
                    }*/

                    stream.Seek(0, SeekOrigin.Begin);
                    string packageHash;
                    using (var sha512 = SHA512.Create())
                    {
                        packageHash = Convert.ToBase64String(sha512.ComputeHash(stream));
                    }

                    // Note: PackageRepository relies on the hash file being written out as the final operation as part of a package install
                    // to assume a package was fully installed.
                    File.WriteAllText(hashPath, packageHash);
                }

                return 0;
            });
        }

        internal static LibraryIdentity CreateLibraryFromNupkg(string nupkgPath)
        {
            using (var fileStream = File.OpenRead(nupkgPath))
            using (var archive = new ZipArchive(fileStream))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.Name.EndsWith(NuGet.Constants.ManifestExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using (var entryStream = entry.Open())
                    {
                        var reader = new NuspecReader(entryStream);
                        return new LibraryIdentity()
                        {
                            Name = reader.GetId(),
                            Version = reader.GetVersion(),
                            Type = LibraryTypes.Package
                        };
                    }
                }

                throw new FormatException(
                    string.Format("{0} doesn't contain {1} entry", nupkgPath, NuGet.Constants.ManifestExtension));
            }
        }

        private static void ExtractPackage(string targetPath, FileStream stream)
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var packOperations = new PublishOperations();
                packOperations.ExtractNupkg(archive, targetPath);
            }
        }
    }
}