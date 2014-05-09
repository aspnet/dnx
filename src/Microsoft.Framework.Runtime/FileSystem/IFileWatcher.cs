// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;

namespace Microsoft.Framework.Runtime.FileSystem
{
    public interface IFileWatcher : IDisposable
    {
        void WatchDirectory(string path, string extension);

        bool WatchFile(string path);
    }
}
