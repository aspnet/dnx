// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet
{
    public interface IPackageSourceProvider
    {
        IEnumerable<PackageSource> LoadPackageSources();
        void SavePackageSources(IEnumerable<PackageSource> sources);
        void DisablePackageSource(PackageSource source);
        bool IsPackageSourceEnabled(PackageSource source);
    }
}