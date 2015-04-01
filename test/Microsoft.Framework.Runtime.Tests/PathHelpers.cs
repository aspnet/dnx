// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Framework.Runtime.Tests
{
    public static class PathHelpers
    {
        public static string GetRootedPath(params string[] paths)
        {
            string root = "/root";

            if (IsWindows())
            {
                root = @"C:\";
            }

            if (!paths.Any())
            {
                return root;
            }

            return Path.Combine(root, paths.Aggregate(Path.Combine));
        }

        private static bool IsWindows()
        {
            var p = (int)Environment.OSVersion.Platform;
            return (p != 4) && (p != 6) && (p != 128);
        }
    }
}
