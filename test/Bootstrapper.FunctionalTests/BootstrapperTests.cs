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
        public static IEnumerable<object[]> RuntimeHomeDirs
        {
            get
            {
                foreach (var path in TestUtils.GetRuntimeHomeDirs())
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("RuntimeHomeDirs")]
        public void BootstrapperReturnsNonZeroExitCodeWhenNoArgumentWasGiven(DisposableDir runtimeHomeDir)
        {
            using (runtimeHomeDir)
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
        [MemberData("RuntimeHomeDirs")]
        public void BootstrapperReturnsZeroExitCodeWhenHelpOptionWasGiven(DisposableDir runtimeHomeDir)
        {
            using (runtimeHomeDir)
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
        [MemberData("RuntimeHomeDirs")]
        public void BootstrapperShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(DisposableDir runtimeHomeDir)
        {
            using (runtimeHomeDir)
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
        [MemberData("RuntimeHomeDirs")]
        public void BootstrapperInvokesApplicationHostWithInferredAppBase(DisposableDir runtimeHomeDir)
        {
            using (runtimeHomeDir)
            using (var samplesPath = TestUtils.GetSamplesFolder())
            {
                var sampleAppRoot = Path.Combine(samplesPath, "HelloWorld");
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
            using (var samplesPath = TestUtils.GetSamplesFolder())
            {
                var sampleAppRoot = Path.Combine(samplesPath, "HelloWorld");

                KpmTestUtils.ExecKpm(
                    runtimeHomeDir,
                    subcommand: "build",
                    arguments: string.Format("{0} --configuration=Release", sampleAppRoot));

                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: Path.Combine(sampleAppRoot, "bin", "Release", "aspnet50", "HelloWorld.dll"),
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
            using (var samplesPath = TestUtils.GetSamplesFolder())
            {
                var sampleAppRoot = Path.Combine(samplesPath, "HelloWorld");

                KpmTestUtils.ExecKpm(
                    runtimeHomeDir,
                    subcommand: "build",
                    arguments: string.Format("{0} --configuration=Release", sampleAppRoot));

                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: Path.Combine(sampleAppRoot, "bin", "Release", "aspnetcore50", "HelloWorld.dll"),
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
