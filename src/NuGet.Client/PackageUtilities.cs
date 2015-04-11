// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.Common;

namespace NuGet
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
            ILogger report)
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
                        report.WriteError(string.Format("The ZIP archive {0} is corrupt",
                            fileStream.Name.Yellow().Bold()));
                    }
                    throw;
                }
            }
        }
    }
}