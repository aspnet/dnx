// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
#if NET45
using System.IO.Packaging;
#endif
using System.Linq;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackOperations
    {
        public void Delete(string folderPath)
        {
            // Calling DeleteRecursive rather than Directory.Delete(..., recursive: true)
            // due to an infrequent exception which can be thrown from that API
            DeleteRecursive(folderPath);
        }

        private void DeleteRecursive(string deletePath)
        {
            if (!Directory.Exists(deletePath))
            {
                return;
            }

            foreach (var deleteFilePath in Directory.EnumerateFiles(deletePath).Select(Path.GetFileName))
            {
                File.Delete(Path.Combine(deletePath, deleteFilePath));
            }

            foreach (var deleteFolderPath in Directory.EnumerateDirectories(deletePath).Select(Path.GetFileName))
            {
                DeleteRecursive(Path.Combine(deletePath, deleteFolderPath));
                Directory.Delete(Path.Combine(deletePath, deleteFolderPath), recursive: true);
            }
        }

        public void Copy(string sourcePath, string targetPath)
        {
            CopyRecursive(
                sourcePath, 
                targetPath, 
                isProjectRootFolder: true, 
                shouldInclude: (_, __) => true);
        }

        public void Copy(string sourcePath, string targetPath, Func<bool, string, bool> shouldInclude)
        {
            CopyRecursive(
                sourcePath, 
                targetPath, 
                isProjectRootFolder: true,
                shouldInclude: shouldInclude);
        }

        private void CopyRecursive(string sourcePath, string targetPath, bool isProjectRootFolder, Func<bool, string, bool> shouldInclude)
        {
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            foreach (var sourceFilePath in Directory.EnumerateFiles(sourcePath))
            {
                var fileName = Path.GetFileName(sourceFilePath);
                Debug.Assert(fileName != null, "fileName != null");

                if (!shouldInclude(isProjectRootFolder, fileName))
                {
                    continue;
                }

                // copy file
                var fullSourcePath = Path.Combine(sourcePath, fileName);
                var fullTargetPath = Path.Combine(targetPath, fileName);

                File.Copy(
                    fullSourcePath,
                    fullTargetPath,
                    overwrite: true);

                // clear read-only bit if set
                var fileAttributes = File.GetAttributes(fullTargetPath);
                if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(fullTargetPath, fileAttributes & ~FileAttributes.ReadOnly);
                }
            }

            foreach (var sourceFolderPath in Directory.EnumerateDirectories(sourcePath))
            {
                var folderName = Path.GetFileName(sourceFolderPath);
                Debug.Assert(folderName != null, "folderName != null");

                if (!shouldInclude(isProjectRootFolder, folderName))
                {
                    continue;
                }

                CopyRecursive(
                    Path.Combine(sourcePath, folderName),
                    Path.Combine(targetPath, folderName),
                    isProjectRootFolder: false,
                    shouldInclude: shouldInclude);
            }
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

#if NET45
        public void ExtractNupkg(Package archive, string targetPath)
        {
            ExtractFiles(
                archive,
                targetPath,
                shouldInclude: NupkgFilter);
        }

        public void ExtractFiles(Package archive, string targetPath, Func<string, bool> shouldInclude)
        {
            foreach (PackagePart part in archive.GetParts())
            {
                var entryFullName = GetPath(part.Uri);
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

                    using (var entryStream = part.GetStream())
                    {
                        using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            entryStream.CopyTo(targetStream);
                        }
                    }
                }
            }
        }
#endif
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
