// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using NuGet;

namespace Microsoft.Dnx.Runtime.Servicing
{
    public static class ServicingTable
    {
        private static ServicingIndex _index;
        private static bool _indexInitialized;
        private static object _indexSync;

        public static bool TryGetReplacement(
            string packageId,
            SemanticVersion packageVersion,
            string assetPath,
            out string replacementPath)
        {
            return LoadIndex().TryGetReplacement(packageId, packageVersion, assetPath, out replacementPath);
        }

        private static ServicingIndex LoadIndex()
        {
            return LazyInitializer.EnsureInitialized(ref _index, ref _indexInitialized, ref _indexSync, () =>
            {
                var index = new ServicingIndex();
                var servicingRoot = GetServicingRoot();
                if (servicingRoot != null)
                {
                    index.Initialize(servicingRoot);
                }

                return index;
            });
        }

        private static string GetServicingRoot()
        {
            return Environment.GetEnvironmentVariable(EnvironmentNames.Servicing)
                ?? GetDefaultServicingRoot();
        }

        private static string GetDefaultServicingRoot()
        {
            var programFiles =
                Environment.GetEnvironmentVariable("PROGRAMFILES(X86)")
                    ?? Environment.GetEnvironmentVariable("PROGRAMFILES");

            return programFiles != null
                ? Path.Combine(programFiles, Constants.RuntimeLongName, "Servicing")
                : null;
        }
    }
}