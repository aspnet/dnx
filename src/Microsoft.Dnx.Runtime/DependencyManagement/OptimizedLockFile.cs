// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace Microsoft.Dnx.Runtime.DependencyManagement
{
    public class OptimizedLockFile
    {
        public LockFile LockFile { get; private set; }
        public ILookup<string, LockFileLibrary> LockFileLibraryLookup { get; private set; }

        public OptimizedLockFile(LockFile lockFile)
        {
            LockFile = lockFile;
            LockFileLibraryLookup = LockFile.Libraries.ToLookup(l => l.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}