// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.FunctionalTestUtils;
using Microsoft.Framework.PackageManager;
using Microsoft.Framework.Runtime;
using Xunit;

namespace Bootstrapper.FunctionalTests
{
    public class BootstrapperTests
    {
        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                return TestUtils.GetRuntimeComponentsCombinations();
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperReturnsNonZeroExitCodeWhenNoArgumentWasGiven(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.NotEqual(0, exitCode);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperReturnsZeroExitCodeWhenHelpOptionWasGiven(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "--help",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "--version",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
                Assert.Contains(TestUtils.GetRuntimeVersion(), stdOut);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void BootstrapperInvokesApplicationHostWithInferredAppBase(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                var sampleAppRoot = Path.Combine(TestUtils.GetSamplesFolder(), "HelloWorld");
                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: string.Format("{0} run", sampleAppRoot),
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
        [InlineData("clr", "win", "x86")]
        [InlineData("clr", "win", "x64")]
        public void BootstrapperInvokesAssemblyWithInferredAppBaseAndLibPathOnClr(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            using (var tempDir = TestUtils.CreateTempDir())
            {
                var samplesPath = TestUtils.GetSamplesFolder();
                var sampleAppRoot = Path.Combine(samplesPath, "HelloWorld");

                var exitCode = KpmTestUtils.ExecKpm(
                    runtimeHomeDir,
                    subcommand: "build",
                    arguments: string.Format("{0} --configuration=Release --out {1}", sampleAppRoot, tempDir.DirPath));

                Assert.Equal(0, exitCode);

                string stdOut, stdErr;
                exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: Path.Combine(tempDir, "Release", "dnx451", "HelloWorld.dll"),
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } });

                Assert.Equal(0, exitCode);
                Assert.Equal(@"Hello World!
Hello, code!
", stdOut);
            }
        }

        [Theory]
        [InlineData("coreclr", "win", "x86")]
        [InlineData("coreclr", "win", "x64")]
        public void BootstrapperInvokesAssemblyWithInferredAppBaseAndLibPathOnCoreClr(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            using (var tempDir = TestUtils.CreateTempDir())
            {
                var samplesPath = TestUtils.GetSamplesFolder();
                var sampleAppRoot = Path.Combine(samplesPath, "HelloWorld");

                var exitCode = KpmTestUtils.ExecKpm(
                    runtimeHomeDir,
                    subcommand: "build",
                    arguments: string.Format("{0} --configuration=Release --out {1}", sampleAppRoot, tempDir.DirPath));

                Assert.Equal(0, exitCode);

                string stdOut, stdErr;
                exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: Path.Combine(tempDir, "Release", "dnxcore50", "HelloWorld.dll"),
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
