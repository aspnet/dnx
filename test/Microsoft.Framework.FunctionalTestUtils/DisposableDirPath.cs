// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Framework.FunctionalTestUtils
{
    public sealed class DisposableDirPath : IDisposable
    {
        private readonly string _dirPath;

        public DisposableDirPath(string dirPath)
        {
            _dirPath = dirPath;
        }

        public static implicit operator string(DisposableDirPath disposableDirPath)
        {
            return disposableDirPath._dirPath;
        }

        public static implicit operator DisposableDirPath(string dirPath)
        {
            return new DisposableDirPath(dirPath);
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
