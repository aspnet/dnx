// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Dnx.Tooling
{
    internal class RestoreProjectsCollector
    {
        public static bool Find(string restoreDirectory, out IEnumerable<string> projectJsonFiles)
        {
            if (string.Equals(Runtime.Project.ProjectFileName,
                              Path.GetFileName(restoreDirectory),
                              StringComparison.OrdinalIgnoreCase) && File.Exists(restoreDirectory))
            {
                // If the path is a project.json file we don't do recursive search in subfolders
                projectJsonFiles = new List<string> { restoreDirectory };

                return true;
            }

            if (!Directory.Exists(restoreDirectory))
            {
                projectJsonFiles = Enumerable.Empty<string>();
                return false;
            }

            var result = new List<string>();
            CollectProjectFiles(restoreDirectory, result);

            projectJsonFiles = result;
            return true;
        }

        private static void CollectProjectFiles(string directory, List<string> results)
        {
            var candidate = Path.Combine(directory, Runtime.Project.ProjectFileName);
            if (File.Exists(candidate))
            {
                results.Add(candidate);
            }
            else
            {
                foreach (var subdirectory in Directory.EnumerateDirectories(directory))
                {
                    CollectProjectFiles(subdirectory, results);
                }
            }
        }
    }
}
