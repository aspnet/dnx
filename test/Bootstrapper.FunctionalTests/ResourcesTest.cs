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
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempDir = new DisposableDir())
            {
                var appPath = Path.Combine(tempDir, "ResourcesTestProjects", "ReadFromResources");
                TestUtils.CreateDisposableTestProject(runtimeHomeDir, Path.Combine(tempDir, "ResourcesTestProjects"), Path.Combine(TestUtils.GetMiscProjectsFolder(), "ResourcesTestProjects", "ReadFromResources"));

                string stdOut;
                string stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "-p . run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: Path.Combine(appPath, "src", "ReadFromResources"));

                Assert.Equal(0, exitCode);
                Assert.Contains(@"Hello World!
Bonjour Monde!
Hallo Welt
Bienvenue
The name '{0}' is ambiguous.
Le nom '{0}' est ambigu.
In TestClass
", stdOut);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void ReadEmbeddedResourcesFromProject(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempDir = new DisposableDir())
            {
                var appPath = Path.Combine(tempDir, "ResourcesTestProjects", "EmbeddedResources");
                TestUtils.CreateDisposableTestProject(runtimeHomeDir, Path.Combine(tempDir, "ResourcesTestProjects"), Path.Combine(TestUtils.GetMiscProjectsFolder(), "ResourcesTestProjects", "EmbeddedResources"));

                string stdOut;
                string stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "-p . run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: appPath);

                Assert.Equal(0, exitCode);
                Assert.Contains(@"Hello
<html>
Basic Test
</html>
", stdOut);
            }
        }
    }
}
