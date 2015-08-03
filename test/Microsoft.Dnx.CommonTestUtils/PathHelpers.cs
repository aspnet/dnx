// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.CommonTestUtils
{
    public static class PathHelpers
    {
        public static string GetRootedPath(params string[] paths)
        {
            string root = "/root";

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                root = @"C:\";
            }

            if (!paths.Any())
            {
                return root;
            }

            return Path.Combine(root, paths.Aggregate((path1, path2) => Path.Combine(path1, path2)));
        }
    }
}
