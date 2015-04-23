// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.CommonTestUtils;
using Microsoft.Framework.PackageManager;
using Microsoft.Framework.Runtime;
using Xunit;

namespace Bootstrapper.FunctionalTests
{
    [Collection("BootstrapperTestCollection")]
    public class BootstrapperTests
    {
        private readonly DnxRuntimeFixture _fixture;

        public BootstrapperTests(DnxRuntimeFixture fixture)
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

        public static IEnumerable<object[]> ClrRuntimeComponents
        {
            get
            {
                return TestUtils.GetClrRuntimeComponents();
            }
        }

        public static IEnumerable<object[]> CoreClrRuntimeComponents
        {
            get
            {
                return TestUtils.GetCoreClrRuntimeComponents();
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperReturnsNonZeroExitCodeWhenNoArgumentWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: string.Empty,
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.NotEqual(0, exitCode);
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperReturnsZeroExitCodeWhenHelpOptionWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: "--help",
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.Equal(0, exitCode);
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: "--version",
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.Equal(0, exitCode);
            Assert.Contains(TestUtils.GetRuntimeVersion(), stdOut);

        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperInvokesApplicationHostWithInferredAppBase_ProjectDirAsArgument(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempSamplesDir = BootstrapperTestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            {
                var testAppPath = Path.Combine(tempSamplesDir, "HelloWorld");

                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: string.Format("{0} run", testAppPath),
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } });

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

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperInvokesApplicationHostWithInferredAppBase_ProjectFileAsArgument(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempSamplesDir = BootstrapperTestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            {
                var testAppPath = Path.Combine(tempSamplesDir, "HelloWorld");
                var testAppProjectFile = Path.Combine(testAppPath, Project.ProjectFileName);

                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: string.Format("{0} run", testAppProjectFile),
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } });

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

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperInvokesAssemblyWithInferredAppBaseAndLibPathOnClr(string flavor, string os, string architecture)
        {
            var outputFolder = flavor == "coreclr" ? "dnxcore50" : "dnx451";
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempSamplesDir = BootstrapperTestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            using (var tempDir = TestUtils.CreateTempDir())
            {
                var sampleAppRoot = Path.Combine(tempSamplesDir, "HelloWorld");

                string stdOut, stdErr;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "build",
                    arguments: string.Format("{0} --configuration=Release --out {1}", sampleAppRoot, tempDir),
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                if (exitCode != 0)
                {
                    Console.WriteLine(stdOut);
                    Console.WriteLine(stdErr);
                }

                exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: Path.Combine(tempDir, "Release", outputFolder, "HelloWorld.dll"),
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } });

                Assert.Equal(0, exitCode);
                Assert.Equal(@"Hello World!
Hello, code!
", stdOut);
            }
        }
    }
}
