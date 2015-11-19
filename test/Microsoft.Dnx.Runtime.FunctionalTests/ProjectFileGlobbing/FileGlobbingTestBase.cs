// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Runtime.FunctionalTests.Utilities;

namespace Microsoft.Dnx.Runtime.FunctionalTests.ProjectFileGlobbing
{
    public abstract class FileGlobbingTestBase : IDisposable
    {
        public FileGlobbingTestBase()
        {
            Root = new DisposableDir();
            CreateContext();
        }

        protected DisposableDir Root { get; private set; }

        public void Dispose()
        {
            if (Root != null)
            {
                Root.Dispose();
                Root = null;
            }
        }

        protected abstract void CreateContext();

        protected abstract ProjectFilesCollection CreateFilesCollection(string jsonContent, string projectDir);

        protected void VerifyFilePathsCollection(IEnumerable<string> actualFiles, params string[] expectFiles)
        {
            var expectFilesInFullpath = expectFiles.Select(relativePath =>
                Path.GetFullPath(Path.Combine(Root.DirPath, PathHelper.NormalizeSeparator(relativePath))));

            var actualFilesInFullpath = actualFiles.Select(filePath =>
                Path.GetFullPath(filePath));

            AssertHelpers.SortAndEqual(expectFilesInFullpath, actualFilesInFullpath, StringComparer.InvariantCultureIgnoreCase);
        }

        protected void AddFiles(params string[] fileRelativePaths)
        {
            foreach (var path in fileRelativePaths)
            {
                var fullPath = Path.Combine(Root.DirPath, path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                File.WriteAllText(
                    fullPath,
                    string.Format("Automatically generated for testing on {0} {1}",
                        DateTime.Now.ToLongDateString(),
                        DateTime.Now.ToLongTimeString()));
            }
        }
    }
}