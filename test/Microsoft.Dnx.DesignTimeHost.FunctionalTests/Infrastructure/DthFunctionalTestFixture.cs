// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Dnx.DesignTimeHost.FunctionalTests.Util;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Testing;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure
{
    public class DthFunctionalTestFixture : IDisposable
    {
        private readonly DisposableDir _context;

        public DthFunctionalTestFixture()
        {
            _context = new DisposableDir();

            PrepareTestProjects();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        public string GetTestProjectPath(string projectName)
        {
            return Path.Combine(_context.DirPath, projectName);
        }

        public IDisposable CreateDisposableTestProject(string projectName, DnxSdk sdk, out string testProjectDir)
        {
            var source = Path.Combine(TestUtils.GetTestSolutionsDirectory(), "DthTestProjects", projectName);
            if (!Directory.Exists(source))
            {
                throw new ArgumentException($"Test project {source} doesn't exist.", nameof(projectName));
            }

            var disposableDir = new DisposableDir();
            CreateDisposableTestProject(sdk, disposableDir, source);

            testProjectDir = Path.Combine(disposableDir, projectName);
            return disposableDir;
        }

        private void PrepareTestProjects()
        {
            var sdk = DnxSdkFunctionalTestBase.ClrDnxSdks.First();
            var dthTestProjectsSource = Path.Combine(TestUtils.GetTestSolutionsDirectory(), "DthTestProjects");

            foreach (var testProject in Directory.GetDirectories(dthTestProjectsSource))
            {
                CreateDisposableTestProject((DnxSdk)sdk[0], _context.DirPath, testProject);
            }
        }

        private static void CreateDisposableTestProject(DnxSdk sdk, string targetDir, string sourceDir)
        {
            // Find the misc project to copy
            var targetProjectDir = Path.Combine(targetDir, Path.GetFileName(sourceDir));
            Testing.TestUtils.CopyFolder(sourceDir, targetProjectDir);

            // Make sure package restore can be successful
            var currentDnxSolutionRootDir = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());

            File.Copy(
                Path.Combine(currentDnxSolutionRootDir, "NuGet.config"),
                Path.Combine(targetProjectDir, "NuGet.config"));

            // Use the newly built runtime to generate lock files for samples
            sdk.Dnu.Restore(targetProjectDir);
        }
    }
}
