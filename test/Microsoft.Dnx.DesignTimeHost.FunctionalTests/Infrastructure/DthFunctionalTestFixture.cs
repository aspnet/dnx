// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Testing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure
{
    public class DthFunctionalTestFixture: IDisposable
    {
        private readonly DisposableDir _context;
        private readonly IDictionary<string, string> _testProjects;

        public DthFunctionalTestFixture()
        {
            _context = new DisposableDir();
            _testProjects = PrepareTestProjects();
        }

        public string ContextDir
        {
            get { return _context.DirPath; }
        }

        public virtual void Dispose()
        {
            _context?.Dispose();
        }

        public string GetTestProjectPath(string projectName)
        {
            return Path.Combine(_context.DirPath, projectName);
        }

        public IDisposable CreateDisposableTestProject(string projectName, string runtimeHomePath, out string testProjectDir)
        {
            var source = Path.Combine(CommonTestUtils.TestUtils.GetMiscProjectsFolder(), "DthTestProjects", projectName);
            if (!Directory.Exists(source))
            {
                throw new ArgumentException($"Test project {source} doesn't exist.", nameof(projectName));
            }

            var disposableDir = new DisposableDir();
            var restoreExitCode = CommonTestUtils.TestUtils.CreateDisposableTestProject(runtimeHomePath, disposableDir, source);
            if (restoreExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to restore project {projectName}");
            }

            testProjectDir = Path.Combine(disposableDir, projectName);
            return disposableDir;
        }

        private IDictionary<string, string> PrepareTestProjects()
        {
            var result = new Dictionary<string, string>();

            var sdk = (DnxSdk)DnxSdkFunctionalTestBase.ClrDnxSdks.First()[0];

            // Prepare test projects in source code
            var dthTestProjectsSource = Path.Combine(CommonTestUtils.TestUtils.GetMiscProjectsFolder(), "DthTestProjects");
            foreach (var testProject in Directory.GetDirectories(dthTestProjectsSource))
            {
                var testProjectName = Path.GetFileName(testProject);
                var target = Path.Combine(_context, "testProjects", testProjectName);

                Testing.TestUtils.CopyFolder(testProject, target);

                var restoreResult = sdk.Dnu.Restore(target);
                restoreResult.EnsureSuccess();

                _testProjects[testProjectName] = target;
            }

            // Generate test package on the fly
            
            
            return result;
        }
        
        private string CreateTestPackages()
        {
            var projectJson = new JObject {
                ["version"] = "0.1.0",
                ["frameworks"] = new JObject {
                    ["dnx451"] = new JObject()
                }
            };
            
            var dir = new Dir {
                ["alpha"] = new Dir {
                    ["projectDir"] = 
                }
                ["output"] = new Dir { }
            };
        }
    }
}
