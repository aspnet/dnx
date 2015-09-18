// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Dnx.Tooling
{
    internal class ClearCacheCommand
    {
        public Reports Reports { get; }
        public string HttpCacheDirectory { get; }

        public ClearCacheCommand(Reports reports, string httpCacheDirectory)
        {
            Reports = reports;
            HttpCacheDirectory = httpCacheDirectory;
        }

        public int Execute()
        {
            if (Directory.Exists(HttpCacheDirectory))
            {
                Reports.Information.WriteLine($"Clearing cache directory {HttpCacheDirectory}");

                try
                {
                    DeleteDirectoryRecursively(HttpCacheDirectory, deleteBaseDirectory: false);
                    Reports.Information.WriteLine("Cache cleared.");
                    return 0;
                }
                catch (Exception e)
                {
                    Reports.Error.WriteLine($"Unable to clear cache directory: {e.Message}");
                    return 1;
                }
            }

            return 0;
        }
        public void DeleteDirectoryRecursively(string baseDirectory, bool deleteBaseDirectory)
        {
            var files = Directory.GetFiles(baseDirectory);
            var subDirectories = Directory.GetDirectories(baseDirectory);

            foreach (var file in files)
            {
                File.Delete(file);
            }

            foreach (var subDirectory in subDirectories)
            {
                DeleteDirectoryRecursively(subDirectory, deleteBaseDirectory: true);
            }

            if (deleteBaseDirectory)
            {
                Directory.Delete(baseDirectory);
            }
        }
    }
}
