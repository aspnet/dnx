// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet
{
    public static class PackageExtensions
    {
        public static bool IsReleaseVersion(this IPackageName packageMetadata)
        {
            return String.IsNullOrEmpty(packageMetadata.Version.SpecialVersion);
        }

        public static string GetFullName(this IPackageName package)
        {
            return package.Id + " " + package.Version;
        }
    }
}
