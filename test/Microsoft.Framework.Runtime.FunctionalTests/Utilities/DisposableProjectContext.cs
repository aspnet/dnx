// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Framework.Runtime.FunctionalTests.Utilities
{
    public class DisposableProjectContext : IDisposable
    {
        private readonly bool _automaticCleanup;

        public DisposableProjectContext(bool automaticCleanup = true)
        {
            _automaticCleanup = automaticCleanup;

            RootPath = Path.GetTempFileName();
            File.Delete(RootPath);
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string ProjectJsonPath { get; private set; }

        public DisposableProjectContext AddProjectJson(string relativePath, string content)
        {
            if (ProjectJsonPath == null)
            {
                var filePath = Path.Combine(RootPath, relativePath, "project.json");
                File.WriteAllText(filePath, content);

                ProjectJsonPath = filePath;
            }
            else
            {
                throw new InvalidOperationException("project.json was added.");
            }

            return this;
        }

        public DisposableProjectContext AddFiles(params string[] fileRelativePaths)
        {
            foreach (var path in fileRelativePaths)
            {
                var fullPath = Path.Combine(RootPath, path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                File.WriteAllText(
                    fullPath,
                    string.Format("Automatically generated for testing on {0} {1}",
                        DateTime.Now.ToLongDateString(),
                        DateTime.Now.ToLongTimeString()));
            }

            return this;
        }

        public void Dispose()
        {
            if (_automaticCleanup)
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}