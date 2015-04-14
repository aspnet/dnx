// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.FunctionalTestUtils;

namespace Microsoft.Framework.PackageManager.FunctionalTests
{
    public class DnuListTestEnvironment : IDisposable
    {
        private readonly DisposableDir _workingDir;

        public DnuListTestEnvironment()
        {
            _workingDir = TestUtils.CreateTempDir();
        }

        public string RootDir
        {
            get { return _workingDir.DirPath; }
        }

        public void Dispose()
        {
        }
    }
}