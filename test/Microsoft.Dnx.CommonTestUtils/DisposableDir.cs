// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.CommonTestUtils
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
                TestUtils.DeleteFolder(DirPath);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);

            if (string.Equals(RuntimeEnvironmentHelper.RuntimeEnvironment.OperatingSystem, "Mac OS X"))
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
