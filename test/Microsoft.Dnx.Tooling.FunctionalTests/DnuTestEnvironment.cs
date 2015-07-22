// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.CommonTestUtils;

namespace Microsoft.Dnx.Tooling
{
    public class DnuTestEnvironment : IDisposable
    {
        private readonly string _projectName;
        private readonly string _outputDirName;
        private readonly string _runtimePath;

        public DnuTestEnvironment(string runtimePath, string projectName = null, string outputDirName = null)
        {
            _projectName = projectName ?? "ProjectName";
            _outputDirName = outputDirName ?? "OutputDirName";
            _runtimePath = runtimePath;
            RootDir = TestUtils.CreateTempDir();
        }

        public string RootDir { get; private set; }

        public string ProjectName
        {
            get
            {
                return _projectName;
            }
        }

        public string ProjectPath
        {
            get
            {
                return Path.Combine(RootDir, ProjectName);
            }
        }

        public string PublishOutputDirName
        {
            get
            {
                return _outputDirName;
            }
        }

        public string PublishOutputDirPath
        {
            get
            {
                return Path.Combine(RootDir, PublishOutputDirName);
            }
        }

        public void Dispose()
        {
            TestUtils.DeleteFolder(RootDir);
        }
    }
}
