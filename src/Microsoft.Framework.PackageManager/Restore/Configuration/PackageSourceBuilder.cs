// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace Microsoft.Framework.PackageManager
{
    internal static class PackageSourceBuilder
    {
        public static readonly string DefaultFeedUrl = "https://www.nuget.org/api/v2/";

        internal static PackageSourceProvider CreateSourceProvider(ISettings settings)
        {
            var defaultPackageSource = new PackageSource(DefaultFeedUrl);

            var packageSourceProvider = new PackageSourceProvider(
                settings,
                new[] { defaultPackageSource },
                new[] { defaultPackageSource },
                new Dictionary<PackageSource, PackageSource> { });

            packageSourceProvider.LoadPackageSources().ToList();

            return packageSourceProvider;
        }
    }
}
