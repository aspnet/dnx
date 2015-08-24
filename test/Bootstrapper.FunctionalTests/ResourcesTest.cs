// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Runtime;
using Xunit;

namespace Bootstrapper.FunctionalTests
{
    [Collection(nameof(BootstrapperTestCollection))]
    public class ResourcesTest
    {
        private readonly DnxRuntimeFixture _fixture;

        public ResourcesTest(DnxRuntimeFixture fixture)
        {
            _fixture = fixture;
        }

        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                return TestUtils.GetRuntimeComponentsCombinations();
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void ReadResourcesFromProjectNugetPackageAndClassLibrary(string flavor, string os, string architecture)
        {
            const string testApp = @"ResourcesTestProjects\ReadFromResources";
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempDir = TestUtils.CreateTempDir())
            {
                var appPath = Path.Combine(tempDir, testApp);
                TestUtils.CreateDisposableTestProject(runtimeHomeDir, Path.Combine(tempDir, "ResourcesTestProjects"), Path.Combine(TestUtils.GetMiscProjectsFolder(), testApp));

                string stdOut;
                string stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "-p . run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: Path.Combine(appPath, "src", "ReadFromResources"));

                Assert.Equal(0, exitCode);
                Assert.Equal(@"Hello World!
Bonjour Monde!
Bienvenue
The name '{0}' is ambiguous.
Le nom '{0}' est ambigu.
", stdOut);
            }
        }
    }
}
