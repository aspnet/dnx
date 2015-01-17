// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using NuGet;

namespace Microsoft.Framework.Runtime.Servicing
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
                // TODO: remove KRE_ env var
                var dotnetServicing = Environment.GetEnvironmentVariable("DOTNET_SERVICING") ?? Environment.GetEnvironmentVariable("KRE_SERVICING");
                if (string.IsNullOrEmpty(dotnetServicing))
                {
                    var servicingRoot = Environment.GetEnvironmentVariable("ProgramFiles") ??
                                        Environment.GetEnvironmentVariable("HOME");

                    dotnetServicing = Path.Combine(
                        servicingRoot,
                        "dotnet",
                        "Servicing");
                }

                index.Initialize(dotnetServicing);
                return index;
            });
        }

    }
}