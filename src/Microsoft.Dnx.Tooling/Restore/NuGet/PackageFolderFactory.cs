// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Tooling.Restore.NuGet;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    internal static class PackageFolderFactory
    {
        public static IPackageFeed CreatePackageFolderFromPath(string path, bool ignoreFailedSources, Reports reports)
        {
            Func<string, bool> containsNupkg = dir => Directory.Exists(dir) &&
                Directory.EnumerateFiles(dir, "*" + Constants.PackageExtension)
                .Where(x => !Path.GetFileNameWithoutExtension(x).EndsWith(".symbols"))
                .Any();

            if (Directory.Exists(path) &&
                (containsNupkg(path) || Directory.EnumerateDirectories(path).Any(x => containsNupkg(x))))
            {
                return new NuGetPackageFolder(path, reports);
            }
            else
            {
                return new PackageFolder(path, ignoreFailedSources, reports);
            }
        }
    }
}