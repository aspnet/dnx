// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;

namespace Microsoft.Framework.Runtime.Compilation
{
    public interface IFileWatcher : IFileMonitor, IDisposable
    {
        void WatchDirectory(string path, string extension);

        bool WatchFile(string path);

        void WatchProject(string path);
    }
}
