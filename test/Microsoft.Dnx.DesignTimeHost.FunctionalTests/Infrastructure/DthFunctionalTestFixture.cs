// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public void PrepareTestProjects()
        {
            var runtime = TestUtils.GetClrRuntimeComponents().First();
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir((string)runtime[0], (string)runtime[1], (string)runtime[2]);

            foreach (var testProject in Directory.GetDirectories(Path.Combine(TestUtils.GetMiscProjectsFolder(), "DthTestProjects")))
            {
                TestUtils.CreateDisposableTestProject(
                    runtimeHomeDir,
                    _context.DirPath,
                    testProject);
            }
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
    }
}
