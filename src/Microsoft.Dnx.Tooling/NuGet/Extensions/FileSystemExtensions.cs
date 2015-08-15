// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Resources;

namespace NuGet
{
    internal static class FileSystemExtensions
    {
        internal static IEnumerable<string> GetFiles(this IFileSystem fileSystem, string path, string filter)
        {
            return fileSystem.GetFiles(path, filter, recursive: false);
        }

        internal static IEnumerable<string> GetDirectoriesSafe(this IFileSystem fileSystem, string path)
        {
            try
            {
                return fileSystem.GetDirectories(path);
            }
            catch (Exception e)
            {
                fileSystem.Logger.Log(MessageLevel.Warning, e.Message);
            }

            return Enumerable.Empty<string>();
        }

        internal static IEnumerable<string> GetFilesSafe(this IFileSystem fileSystem, string path)
        {
            return GetFilesSafe(fileSystem, path, "*.*");
        }

        internal static IEnumerable<string> GetFilesSafe(this IFileSystem fileSystem, string path, string filter)
        {
            try
            {
                return fileSystem.GetFiles(path, filter);
            }
            catch (Exception e)
            {
                fileSystem.Logger.Log(MessageLevel.Warning, e.Message);
            }

            return Enumerable.Empty<string>();
        }
    }
}