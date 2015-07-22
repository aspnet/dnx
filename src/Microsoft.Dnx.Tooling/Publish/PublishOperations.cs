// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishOperations
    {
        public void Delete(string folderPath)
        {
            FileOperationUtils.DeleteFolder(folderPath);
        }

        public void DeleteEmptyFolders(string packageDir)
        {
            DeleteEmptyFolders(new DirectoryInfo(packageDir));
        }

        private void DeleteEmptyFolders(DirectoryInfo directoryInfo)
        {
            foreach (var directory in directoryInfo.EnumerateDirectories())
            {
                DeleteEmptyFolders(directory);
            }

            if (!directoryInfo.EnumerateFileSystemInfos().Any())
            {
                directoryInfo.Delete();
            }
        }

        public void Copy(IEnumerable<string> sourceFiles, string sourceDirectory, string targetDirectory)
        {
            if (sourceFiles == null)
            {
                throw new ArgumentNullException(nameof(sourceFiles));
            }

            sourceDirectory = PathUtility.EnsureTrailingSlash(sourceDirectory);
            targetDirectory = PathUtility.EnsureTrailingSlash(targetDirectory);

            foreach (var sourceFilePath in sourceFiles)
            {
                var fileName = Path.GetFileName(sourceFilePath);
                Debug.Assert(fileName != null, "fileName != null");

                var targetFilePath = sourceFilePath.Replace(sourceDirectory, targetDirectory);
                var targetFileParentFolder = Path.GetDirectoryName(targetFilePath);

                // Create directory before copying a file
                if (!Directory.Exists(targetFileParentFolder))
                {
                    Directory.CreateDirectory(targetFileParentFolder);
                }

                File.Copy(
                    sourceFilePath,
                    targetFilePath,
                    overwrite: true);

                // clear read-only bit if set
                var fileAttributes = File.GetAttributes(targetFilePath);
                if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(targetFilePath, fileAttributes & ~FileAttributes.ReadOnly);
                }
            }
        }

        public void Copy(string sourcePath, string targetPath)
        {
            sourcePath = PathUtility.EnsureTrailingSlash(sourcePath);
            targetPath = PathUtility.EnsureTrailingSlash(targetPath);

            // Directory.EnumerateFiles(path) throws "path is too long" exception if path is longer than 248 characters
            // So we flatten the enumeration here with SearchOption.AllDirectories,
            // instead of calling Directory.EnumerateFiles(path) with SearchOption.TopDirectoryOnly in each subfolder
            foreach (var sourceFilePath in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(sourceFilePath);
                Debug.Assert(fileName != null, "fileName != null");

                var targetFilePath = sourceFilePath.Replace(sourcePath, targetPath);
                var targetFileParentFolder = Path.GetDirectoryName(targetFilePath);

                // Create directory before copying a file
                if (!Directory.Exists(targetFileParentFolder))
                {
                    Directory.CreateDirectory(targetFileParentFolder);
                }

                File.Copy(
                    sourceFilePath,
                    targetFilePath,
                    overwrite: true);

                // clear read-only bit if set
                var fileAttributes = File.GetAttributes(targetFilePath);
                if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(targetFilePath, fileAttributes & ~FileAttributes.ReadOnly);
                }
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
