// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Dnx.Tooling.Publish;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Internal;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    internal static class NuGetPackageUtils
    {
        internal static async Task InstallFromStream(
            Stream stream,
            LibraryIdentity library,
            string packagesDirectory,
            IReport information,
            string packageHash = null)
        {
            var packagePathResolver = new DefaultPackagePathResolver(packagesDirectory);

            var targetPath = packagePathResolver.GetInstallPath(library.Name, library.Version);
            var targetNuspec = packagePathResolver.GetManifestFilePath(library.Name, library.Version);
            var targetNupkg = packagePathResolver.GetPackageFilePath(library.Name, library.Version);
            var targetHashPath = packagePathResolver.GetHashPath(library.Name, library.Version);

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            await ConcurrencyUtilities.ExecuteWithFileLocked(targetNupkg, action: async _ =>
            {
                if (string.IsNullOrEmpty(packageHash))
                {
                    using (var sha512 = SHA512.Create())
                    {
                        packageHash = Convert.ToBase64String(sha512.ComputeHash(stream));
                    }
                }

                var actionName = "Installing";
                var installedPackageHash = string.Empty;
                if (File.Exists(targetHashPath) && File.Exists(targetNuspec))
                {
                    installedPackageHash = File.ReadAllText(targetHashPath);
                    actionName = "Overwriting";
                }

                if (string.Equals(packageHash, installedPackageHash, StringComparison.Ordinal))
                {
                    information.WriteLine($"{library.Name}.{library.Version} already exists");
                }
                else
                {
                    information.WriteLine($"{actionName} {library.Name}.{library.Version}");

                    Directory.CreateDirectory(targetPath);
                    using (var nupkgStream = new FileStream(
                        targetNupkg,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete,
                        bufferSize: 4096,
                        useAsync: true))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        await stream.CopyToAsync(nupkgStream);
                        nupkgStream.Seek(0, SeekOrigin.Begin);

                        ExtractPackage(targetPath, nupkgStream);
                    }

                    // Fixup the casing of the nuspec on disk to match what we expect
                    var nuspecFile = Directory.EnumerateFiles(targetPath, "*" + NuGet.Constants.ManifestExtension).Single();

                    Manifest manifest = null;
                    using (var nuspecStream = File.OpenRead(nuspecFile))
                    {
                        manifest = Manifest.ReadFrom(nuspecStream, validateSchema: false);
                        manifest.Metadata.Id = library.Name;
                        manifest.Metadata.Version = library.Version;
                    }

                    // Delete the previous nuspec file
                    File.Delete(nuspecFile);

                    // Write the new manifest
                    using (var targetNuspecStream = File.OpenWrite(targetNuspec))
                    {
                        manifest.Save(targetNuspecStream);
                    }

                    // Note: PackageRepository relies on the hash file being written out as the final operation as part of a package install
                    // to assume a package was fully installed.
                    File.WriteAllText(targetHashPath, packageHash);
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
                        var manifest = Manifest.ReadFrom(entryStream, validateSchema: false);
                        return new LibraryIdentity(manifest.Metadata.Id, manifest.Metadata.Version, isGacOrFrameworkReference: false);
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