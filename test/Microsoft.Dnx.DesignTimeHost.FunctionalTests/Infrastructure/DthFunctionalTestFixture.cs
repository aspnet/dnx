// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure
{
    public class DthFunctionalTestFixture : DnxRuntimeFixture
    {
        private readonly DisposableDir _context;

        public DthFunctionalTestFixture() : base()
        {
            _context = new DisposableDir();

            PrepareTestProjects();
        }

        public override void Dispose()
        {
            _context?.Dispose();

            base.Dispose();
        }

        public string GetTestProjectPath(string projectName)
        {
            return Path.Combine(_context.DirPath, projectName);
        }

        public IDisposable CreateDisposableTestProject(string projectName, string runtimeHomePath, out string testProjectDir)
        {
            var source = Path.Combine(TestUtils.GetMiscProjectsFolder(), "DthTestProjects", projectName);
            if (!Directory.Exists(source))
            {
                throw new ArgumentException($"Test project {source} doesn't exist.", nameof(projectName));
            }

            var disposableDir = new DisposableDir();
            var restoreExitCode = TestUtils.CreateDisposableTestProject(runtimeHomePath, disposableDir, source);
            if (restoreExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to restore project {projectName}");
            }

            testProjectDir = Path.Combine(disposableDir, projectName);
            return disposableDir;
        }

        private void PrepareTestProjects()
        {
            var runtime = TestUtils.GetClrRuntimeComponents().First();
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir((string)runtime[0], (string)runtime[1], (string)runtime[2]);
            var dthTestProjectsSource = Path.Combine(TestUtils.GetMiscProjectsFolder(), "DthTestProjects");

            foreach (var testProject in Directory.GetDirectories(dthTestProjectsSource))
            {
                TestUtils.CreateDisposableTestProject(
                    runtimeHomeDir,
                    _context.DirPath,
                    testProject);
            }
        }
    }
}
