// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Framework.FunctionalTestUtils
{
    public sealed class DisposableDir : IDisposable
    {
        private readonly string _dirPath;

        public DisposableDir(string dirPath)
        {
            _dirPath = dirPath;
        }

        public static implicit operator string(DisposableDir disposableDirPath)
        {
            return disposableDirPath._dirPath;
        }

        public static implicit operator DisposableDir(string dirPath)
        {
            return new DisposableDir(dirPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dirPath))
            {
                TestUtils.DeleteFolder(_dirPath);
            }
        }
    }
}
