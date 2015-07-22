// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Dnx.CommonTestUtils
{
    public class DnxRuntimeFixture : IDisposable
    {
        private Dictionary<Tuple<string, string, string>, DisposableDir> _runtimeDirs = 
            new Dictionary<Tuple<string, string, string>, DisposableDir>();

        public virtual void Dispose()
        {
            foreach (var runtimeDir in _runtimeDirs.Values)
            {
                runtimeDir.Dispose();
            }
        }

        public string GetRuntimeHomeDir(string flavor, string os, string architecture)
        {
            var key = Tuple.Create(flavor, os, architecture);

            DisposableDir runtimeDir;
            if (!_runtimeDirs.TryGetValue(key, out runtimeDir))
            {
                runtimeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
                _runtimeDirs[key] = runtimeDir;
            }

            return runtimeDir.DirPath;
        }
    }
}
