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
                var dotnetServicing = Environment.GetEnvironmentVariable(EnvironmentNames.Servicing);
                if (string.IsNullOrEmpty(dotnetServicing))
                {
                    var servicingRoot = Environment.GetEnvironmentVariable("PROGRAMFILES(X86)");
                    if (string.IsNullOrEmpty(servicingRoot))
                    {
                        Environment.GetEnvironmentVariable("PROGRAMFILES");
                    }

                    if (string.IsNullOrEmpty(servicingRoot))
                    {
                        // Nothing to do, we don't have Program Files. Just return the uninitialized index.
                        return index;
                    }

                    dotnetServicing = Path.Combine(
                        servicingRoot,
                        "KRE",
                        "Servicing");
                }

                index.Initialize(dotnetServicing);
                return index;
            });
        }

    }
}