// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Dnx.Tooling.Restore.NuGet
{
    public interface IPackageFeed
    {
        string Source { get; }
        Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id);
        Task<Stream> OpenNupkgStreamAsync(PackageInfo package);
        Task<Stream> OpenNuspecStreamAsync(PackageInfo package);
        Task<Stream> OpenRuntimeStreamAsync(PackageInfo package);
    }
}
