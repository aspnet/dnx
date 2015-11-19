// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet;

namespace Microsoft.Dnx.Compilation
{
    internal static class ResourcePathUtility
    {
        public static string GetResourceName(string projectFolder, string resourcePath)
        {
            // If the file is outside of the project folder, we are assuming it is directly in the root
            // otherwise, keep the folders that are inside the project
            return PathUtility.IsChildOfDirectory(projectFolder, resourcePath) ?
                PathUtility.GetRelativePath(projectFolder, resourcePath) :
                Path.GetFileName(resourcePath);
        }
    }
}