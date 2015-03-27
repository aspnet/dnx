// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.Framework.PackageManager.Bundle
{
    public class BundleOperations
    {
        public void Delete(string folderPath)
        {
            FileOperationUtils.DeleteFolder(folderPath);
        }

        public void ExtractNupkg(ZipArchive archive, string targetPath)
        {
            ExtractFiles(
                archive, 
                targetPath, 
                shouldInclude: NupkgFilter);
        }

        private static bool NupkgFilter(string fullName)
        {
            var fileName = Path.GetFileName(fullName);
            if (fileName != null)
            {
                if (fileName == ".rels")
                {
                    return false;
                }
                if (fileName == "[Content_Types].xml")
                {
                    return false;
                }
            }

            var extension = Path.GetExtension(fullName);
            if (extension == ".psmdcp")
            {
                return false;
            }

            return true;
        }

        public void ExtractFiles(ZipArchive archive, string targetPath, Func<string, bool> shouldInclude)
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                var entryFullName = entry.FullName;
                if (entryFullName.StartsWith("/", StringComparison.Ordinal))
                {
                    entryFullName = entryFullName.Substring(1);
                }
                entryFullName = Uri.UnescapeDataString(entryFullName.Replace('/', Path.DirectorySeparatorChar));


                var targetFile = Path.Combine(targetPath, entryFullName);
                if (!targetFile.StartsWith(targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!shouldInclude(entryFullName))
                {
                    continue;
                }

                if (Path.GetFileName(targetFile).Length == 0)
                {
                    Directory.CreateDirectory(targetFile);
                }
                else
                {
                    var targetEntryPath = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetEntryPath))
                    {
                        Directory.CreateDirectory(targetEntryPath);
                    }

                    using (var entryStream = entry.Open())
                    {
                        using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            entryStream.CopyTo(targetStream);
                        }
                    }
                }
            }
        }

        public void AddFiles(ZipArchive archive, string sourcePath, string targetPath, Func<string, string, bool> shouldInclude)
        {
            AddFilesRecursive(
                archive, 
                sourcePath, 
                "", 
                targetPath,
                shouldInclude);
        }

        private void AddFilesRecursive(ZipArchive archive, string sourceBasePath, string sourcePath, string targetPath, Func<string, string, bool> shouldInclude)
        {
            foreach (var fileName in Directory.EnumerateFiles(Path.Combine(sourceBasePath, sourcePath)).Select(Path.GetFileName))
            {
                if (!shouldInclude(sourcePath, fileName))
                {
                    continue;
                }
                var entry = archive.CreateEntry(Path.Combine(targetPath, fileName));
                using (var entryStream = entry.Open())
                {
                    using (var sourceStream = new FileStream(Path.Combine(sourceBasePath, sourcePath, fileName), FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        sourceStream.CopyTo(entryStream);
                    }
                }
            }

            foreach (var folderName in Directory.EnumerateDirectories(Path.Combine(sourceBasePath, sourcePath)).Select(Path.GetFileName))
            {
                AddFilesRecursive(
                    archive,
                    sourceBasePath,
                    Path.Combine(sourcePath, folderName),
                    Path.Combine(targetPath, folderName),
                    shouldInclude);
            }
        }

        private static string GetPath(Uri uri)
        {
            string path = uri.OriginalString;
            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            // Bug 483: We need the unescaped uri string to ensure that all characters are valid for a path.
            // Change the direction of the slashes to match the filesystem.
            return Uri.UnescapeDataString(path.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
