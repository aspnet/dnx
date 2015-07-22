// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling
{
    public static class FileOperationUtils
    {
        public static void DeleteFolder(string folderPath)
        {
            // Calling DeleteRecursive rather than Directory.Delete(..., recursive: true)
            // due to an infrequent exception which can be thrown from that API
            DeleteRecursive(folderPath);
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
            if (RuntimeEnvironmentHelper.IsWindows)
            {
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

        public static void MakeWritable(string filePath)
        {
            var attributes = File.GetAttributes(filePath);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                attributes &= ~FileAttributes.ReadOnly;
                File.SetAttributes(filePath, attributes);
            }
        }
    }
}