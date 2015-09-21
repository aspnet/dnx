// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Dnx.Tooling
{
    internal class ClearCacheCommand
    {
        public ClearCacheCommand(Reports reports, string httpCacheDirectory)
        {
            Reports = reports;
            HttpCacheDirectory = httpCacheDirectory;
        }

        public Reports Reports { get; }
        public string HttpCacheDirectory { get; }

        public int Execute()
        {
            if (Directory.Exists(HttpCacheDirectory))
            {
                Reports.Information.WriteLine($"Clearing cache directory {HttpCacheDirectory}");

                try
                {
                    FileOperationUtils.DeleteFolder(HttpCacheDirectory);
                    Reports.Information.WriteLine("Cache cleared.");
                }
                catch (Exception e)
                {
                    Reports.Error.WriteLine($"Unable to clear cache directory: {e.Message}");
                    return 1;
                }
            }

            return 0;
        }
    }
}
