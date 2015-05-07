// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.Framework.PackageManager
{
    internal static class PackageUtilities
    {
        internal static ZipArchiveEntry GetEntryOrdinalIgnoreCase(this ZipArchive archive, string entryName)
        {
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        internal static async Task<Stream> OpenNuspecStreamFromNupkgAsync(PackageInfo package,
            Func<PackageInfo, Task<Stream>> openNupkgStreamAsync,
            IReport report)
        {
            using (var nupkgStream = await openNupkgStreamAsync(package))
            {
                try {
                    using (var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, leaveOpen: true))
                    {
                        var entry = archive.GetEntryOrdinalIgnoreCase(package.Id + ".nuspec");
                        using (var entryStream = entry.Open())
                        {
                            var nuspecStream = new MemoryStream((int)entry.Length);
#if DNXCORE50
                            // System.IO.Compression.DeflateStream throws exception when multiple
                            // async readers/writers are working on a single instance of it
                            entryStream.CopyTo(nuspecStream);
#else
                            await entryStream.CopyToAsync(nuspecStream);
#endif
                            nuspecStream.Seek(0, SeekOrigin.Begin);
                            return nuspecStream;
                        }
                    }
                }
                catch (InvalidDataException)
                {
                    var fileStream = nupkgStream as FileStream;
                    if (fileStream != null)
                    {
                        report.WriteLine("The ZIP archive {0} is corrupt",
                            fileStream.Name.Yellow().Bold());
                    }
                    throw;
                }
            }
        }

        internal static async Task<Stream> OpenRuntimeStreamFromNupkgAsync(PackageInfo package,
            Func<PackageInfo, Task<Stream>> openNupkgStreamAsync,
            IReport report)
        {
            using (var nupkgStream = await openNupkgStreamAsync(package))
            {
                try
                {
                    using (var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, leaveOpen: true))
                    {
                        var entry = archive.GetEntryOrdinalIgnoreCase("runtime.json");
                        if (entry == null)
                        {
                            return null;
                        }
                        using (var entryStream = entry.Open())
                        {
                            var runtimeStream = new MemoryStream((int)entry.Length);
#if DNXCORE50
                            // System.IO.Compression.DeflateStream throws exception when multiple
                            // async readers/writers are working on a single instance of it
                            entryStream.CopyTo(runtimeStream);
#else
                            await entryStream.CopyToAsync(runtimeStream);
#endif
                            runtimeStream.Seek(0, SeekOrigin.Begin);
                            return runtimeStream;
                        }
                    }
                }
                catch (InvalidDataException)
                {
                    var fileStream = nupkgStream as FileStream;
                    if (fileStream != null)
                    {
                        report.WriteLine("The ZIP archive {0} is corrupt",
                            fileStream.Name.Yellow().Bold());
                    }
                    throw;
                }
            }
        }

        internal static async Task<bool> IsValidNupkgAsync(string packageId, string path)
        {
            // Acquire the lock on a file before we open it to prevent this process from opening
            // a file deleted by HttpSource.GetAsync()/HttpSource.InvalidateCacheFile() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(path, _ =>
            {
                if (!File.Exists(path))
                {
                    return Task.FromResult(false);
                }

                using (var stream = File.OpenRead(path))
                {
                    try {
                        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
                        {
                            var entry = archive.GetEntryOrdinalIgnoreCase(packageId + ".nuspec");
                            return Task.FromResult(entry != null);
                        }
                    }
                    catch (InvalidDataException)
                    {
                        return Task.FromResult(false);
                    }
                }
            });
        }
    }
}
