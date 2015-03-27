// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public static class FileOperationUtils
    {
        public static void DeleteFolder(string folderPath)
        {
            // Calling DeleteRecursive rather than Directory.Delete(..., recursive: true)
            // due to an infrequent exception which can be thrown from that API
            DeleteRecursive(folderPath);
            Directory.Delete(folderPath);
        }

        private static void DeleteRecursive(string deletePath)
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

        public static bool MarkExecutable(string file)
        {
            if (PlatformHelper.IsWindows)
            {
                // This makes sense only on non Windows machines
                return false;
            }

            var processStartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                FileName = "chmod",
                Arguments = string.Format("+x \"{0}\"", file)
            };

            var process = Process.Start(processStartInfo);
            process.WaitForExit();

            return process.ExitCode == 0;
        }

        public static void Copy(string sourcePath, string targetPath)
        {
            Copy(
                sourcePath,
                targetPath,
                shouldInclude: _ => true);
        }

        public static void Copy(string sourcePath, string targetPath, Func<string, bool> shouldInclude)
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

                if (!shouldInclude(sourceFilePath))
                {
                    continue;
                }

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
    }
}