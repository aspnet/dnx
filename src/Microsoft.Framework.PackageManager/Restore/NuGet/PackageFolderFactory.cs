// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Framework.PackageManager.Restore.NuGet;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    internal static class PackageFolderFactory
    {
        public static IPackageFeed CreatePackageFolderFromPath(string path, IReport report)
        {
            Func<string, bool> containsNupkg = dir => Directory.EnumerateFiles(dir, "*" + Constants.PackageExtension)
                .Where(x => !Path.GetFileNameWithoutExtension(x).EndsWith(".symbols"))
                .Any();

            if (containsNupkg(path) || Directory.EnumerateDirectories(path).Any(x => containsNupkg(x)))
            {
                return new NuGetPackageFolder(path, report);
            }
            else
            {
                return new KpmPackageFolder(path, report);
            }
        }
    }
}