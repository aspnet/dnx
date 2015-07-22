// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NuGet;

namespace Microsoft.Dnx.Runtime.Servicing
{
    public static class ServicingTable
    {
        private static List<ServicingIndex> _index;
        private static bool _indexInitialized;
        private static object _indexSync;

        public static bool TryGetReplacement(
            string packageId,
            SemanticVersion packageVersion,
            string assetPath,
            out string replacementPath)
        {
            var compositeIndex = LoadIndex();

            foreach (var index in compositeIndex)
            {
                if (index.TryGetReplacement(packageId, packageVersion, assetPath, out replacementPath))
                {
                    return true;
                }
            }

            replacementPath = null;
            return false;
        }

        private static List<ServicingIndex> LoadIndex()
        {
            return LazyInitializer.EnsureInitialized(ref _index, ref _indexInitialized, ref _indexSync, () =>
            {
                var compositeIndex = new List<ServicingIndex>();

                foreach (var servicingRoot in GetServicingRoots())
                {
                    var index = new ServicingIndex();
                    if (servicingRoot != null && servicingRoot.IndexOfAny(Path.GetInvalidPathChars()) == -1)
                    {
                        index.Initialize(servicingRoot);
                    }

                    compositeIndex.Add(index);
                }

                return compositeIndex;
            });
        }

        private static IEnumerable<string> GetServicingRoots()
        {
            var servicingRoot = Environment.GetEnvironmentVariable(EnvironmentNames.Servicing);

            if (servicingRoot != null)
            {
                yield return servicingRoot;
            }

            yield return GetDefaultServicingRoot();
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