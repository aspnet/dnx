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
    [Collection("BootstrapperTestCollection")]
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

        [Fact]
        public void ReadResourcesFromProjectNugetPackageAndClassLibrary()
        {
            string flavor = "clr";
            string os = "win";
            string architecture = "x86";
            const string testApp = @"ResourcesTestProjects\ReadFromResources\src\ReadFromResources";
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempDir = TestUtils.CreateTempDir())
            {
                System.Console.WriteLine(tempDir);
                var appPath = Path.Combine(tempDir, testApp);
                System.Console.WriteLine(appPath);
                TestUtils.CopyFolder(Path.Combine(TestUtils.GetMiscProjectsFolder(), testApp), appPath);

                string stdOut;
                string stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "-p . run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: appPath);

                Assert.Equal(0, exitCode);
                Assert.Equal(@"Hello World!
Hello, code!
I
can
customize
the
default
command
", stdOut);
            }
        }
    }
}
