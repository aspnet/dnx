// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.Dnx.Tooling.Restore.NuGet
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

        public static async Task<NupkgEntry> OpenNupkgStreamAsync(
            HttpSource httpSource,
            PackageInfo package,
            TimeSpan cacheAgeLimit,
            Reports reports)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    using (var data = await httpSource.GetAsync(
                        package.ContentUri,
                        cacheKey: $"nupkg_{package.Id}.{package.Version}",
                        cacheAgeLimit: retry == 0 ? cacheAgeLimit : TimeSpan.Zero,
                        ensureValidContents: stream => EnsureValidPackageContents(stream, package)))
                    {
                        return new NupkgEntry
                        {
                            TempFileName = data.CacheFileName
                        };
                    }
                }
                catch (Exception ex)
                {
                    var isFinalAttempt = (retry == 2);
                    var message = ex.Message;
                    if (ex is TaskCanceledException)
                    {
                        message = ErrorMessageUtils.GetFriendlyTimeoutErrorMessage(ex as TaskCanceledException, isFinalAttempt, ignoreFailure: false);
                    }

                    if (isFinalAttempt)
                    {
                        reports.Error.WriteLine(
                            $"Error: DownloadPackageAsync: {package.ContentUri}{Environment.NewLine}  {message}".Red().Bold());
                        throw;
                    }
                    else
                    {
                        reports.Information.WriteLine(
                            $"Warning: DownloadPackageAsync: {package.ContentUri}{Environment.NewLine}  {message}".Yellow().Bold());
                    }
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

        internal static void EnsureValidPackageContents(Stream stream, PackageInfo package)
        {
            var message = $"Response from {package.ContentUri} is not a valid NuGet package.";
            try
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var entryName = $"{package.Id}.nuspec";
                    var entry = archive.GetEntryOrdinalIgnoreCase(entryName);
                    if (entry == null)
                    {
                        throw new InvalidDataException($"{message} Cannot find required entry {entryName}.");
                    }
                }
            }
            catch (InvalidDataException e)
            {
                throw new InvalidDataException(message, innerException: e);
            }
        }

    }
}
