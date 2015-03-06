// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Framework.PackageManager.Bundle;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    internal static class NuGetPackageUtils
    {
        internal static async Task InstallFromStream(Stream stream, Library library, string packagesDirectory,
            SHA512 sha512)
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
                    // Extracting to the {id}/{version} has an issue with concurrent installs - when a package has been partially
                    // extracted, the Restore Operation can inadvertly conclude the package is available locally and proceed to read
                    // partially written package contents. To avoid this we'll extract the package to a temporary directory and Move it 
                    // to the target path.
                    var extractPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    Directory.CreateDirectory(extractPath);
                    targetNupkg = Path.Combine(extractPath, Path.GetFileName(targetNupkg));
                    using (var nupkgStream = new FileStream(targetNupkg, FileMode.Create, FileAccess.ReadWrite))
                    {
                        await stream.CopyToAsync(nupkgStream);
                        nupkgStream.Seek(0, SeekOrigin.Begin);

                        ExtractPackage(extractPath, nupkgStream);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    Directory.Move(extractPath, targetPath);

                    // Fixup the casing of the nuspec on disk to match what we expect
                    var nuspecFile = Directory.EnumerateFiles(targetPath, "*" + NuGet.Constants.ManifestExtension).Single();

                    if (!string.Equals(nuspecFile, targetNuspec, StringComparison.Ordinal))
                    {
                        Manifest manifest = null;
                        using (var nuspecStream = File.OpenRead(nuspecFile))
                        {
                            manifest = Manifest.ReadFrom(nuspecStream, validateSchema: false);
                            manifest.Metadata.Id = library.Name;
                        }

                        // Delete the previous nuspec file
                        File.Delete(nuspecFile);

                        // Write the new manifest
                        using (var targetNuspecStream = File.OpenWrite(targetNuspec))
                        {
                            manifest.Save(targetNuspecStream);
                        }
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                    var nupkgSHA = Convert.ToBase64String(sha512.ComputeHash(stream));
                    File.WriteAllText(hashPath, nupkgSHA);
                }

                return 0;
            });
        }

        internal static Library CreateLibraryFromNupkg(string nupkgPath)
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
                        var manifest = Manifest.ReadFrom(entryStream, validateSchema: false);
                        return new Library()
                        {
                            Name = manifest.Metadata.Id,
                            Version = SemanticVersion.Parse(manifest.Metadata.Version)
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
                var packOperations = new BundleOperations();
                packOperations.ExtractNupkg(archive, targetPath);
            }
        }
    }
}