// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Util
{
    public sealed class DisposableDir : IDisposable
    {
        private readonly bool _deleteOnDispose = true;

        public DisposableDir()
            : this(CreateTemporaryDirectory())
        {
        }

        public DisposableDir(string dirPath, bool deleteOnDispose)
        {
            DirPath = dirPath;
            _deleteOnDispose = deleteOnDispose;
        }

        public DisposableDir(string dirPath)
        {
            DirPath = dirPath;
            _deleteOnDispose = !string.Equals(Environment.GetEnvironmentVariable("DNX_KEEP_TEST_DIRS"), "1", StringComparison.Ordinal);
        }

        public string DirPath { get; private set; }

        public static implicit operator string (DisposableDir disposableDirPath)
        {
            return disposableDirPath.DirPath;
        }

        public static implicit operator DisposableDir(string dirPath)
        {
            return new DisposableDir(dirPath);
        }

        public override string ToString()
        {
            return DirPath;
        }

        public void Dispose()
        {
            if (!_deleteOnDispose)
            {
                return;
            }

            if (Directory.Exists(DirPath))
            {
                DeleteFolder(DirPath);
            }
        }

        public static void DeleteFolder(string path)
        {
            var retryNum = 3;
            for (int i = 0; i < retryNum; i++)
            {
                try
                {
                    DeleteFolderInternal(path);
                    return;
                }
                catch (Exception)
                {
                    if (i == retryNum - 1)
                    {
                        throw;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }

        private static void DeleteFolderInternal(string folder)
        {
            foreach (var subfolder in Directory.GetDirectories(folder))
            {
                DeleteFolderInternal(subfolder);
            }

            foreach (var fileName in Directory.GetFiles(folder))
            {
                var fullFilePath = Path.Combine(folder, fileName);

                // Make sure the files are not readonly.
                // Otherwise, Directory.Delete cannot delete them
                File.SetAttributes(fullFilePath, FileAttributes.Normal);
                File.Delete(fullFilePath);
            }

            Directory.Delete(folder, recursive: false);
        }

        private static string CreateTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);

            if (string.Equals(RuntimeEnvironmentHelper.RuntimeEnvironment.OperatingSystem, "Darwin"))
            {
                // Resolves issues on Mac where GetTempPath gives /var and GetCurrentDirectory gives /private/var
                var currentDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(tempDirectory);
                tempDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(currentDirectory);
            }

            return tempDirectory;
        }
    }
}
