// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Framework.FunctionalTestUtils
{
    public sealed class DisposableDir : IDisposable
    {
        public DisposableDir()
            :this(CreateTemporaryDirectory())
        {
        }

        public DisposableDir(string dirPath)
        {
            DirPath = dirPath;
        }

        public string DirPath { get; private set; }

        public static implicit operator string(DisposableDir disposableDirPath)
        {
            return disposableDirPath.DirPath;
        }

        public static implicit operator DisposableDir(string dirPath)
        {
            return new DisposableDir(dirPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirPath))
            {
                TestUtils.DeleteFolder(DirPath);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }
}
