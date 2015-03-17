// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.FunctionalTestUtils;
using Microsoft.Framework.Runtime;
using Xunit;

namespace Microsoft.Framework.ApplicationHost
{
    public class KCommandTests
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
        public void KCommandReturnsNonZeroExitCodeWhenNoArgumentWasGiven(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    runtimeHomeDir,
                    subcommand: string.Empty,
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.NotEqual(0, exitCode);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void KCommandReturnsZeroExitCodeWhenHelpOptionWasGiven(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    runtimeHomeDir,
                    subcommand: string.Empty,
                    arguments: "--help",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void KCommandShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    runtimeHomeDir,
                    subcommand: string.Empty,
                    arguments: "--version",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
                Assert.Contains(TestUtils.GetRuntimeVersion(), stdOut);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void KCommandShowsErrorWhenNoProjectJsonWasFound(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            using (var emptyFolder = TestUtils.CreateTempDir())
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    runtimeHomeDir,
                    subcommand: "run",
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.AppBase, emptyFolder } });

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Unable to resolve project", stdErr);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void KCommandShowsErrorWhenGivenSubcommandWasNotFoundInProjectJson(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            var projectStructure = @"{
  'project.json': '{ }'
}";

            using (runtimeHomeDir)
            using (var projectPath = TestUtils.CreateTempDir())
            {

                DirTree.CreateFromJson(projectStructure).WriteTo(projectPath);

                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    runtimeHomeDir,
                    subcommand: "invalid",
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.AppBase, projectPath } });

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Unable to load application or execute command 'invalid'.", stdErr);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void KCommandRunsSampleAppGivenAppBase(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                var sampleAppRoot = Path.Combine(TestUtils.GetSamplesFolder(), "HelloWorld");
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    runtimeHomeDir,
                    subcommand: "run",
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.AppBase, sampleAppRoot } });

                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void KCommandRunsSampleAppUsingDefaultAppBase(string flavor, string os, string architecture)
        {
            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            {
                var sampleAppRoot = Path.Combine(TestUtils.GetSamplesFolder(), "HelloWorld");
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    runtimeHomeDir,
                    subcommand: "run",
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.AppBase, null } },
                    workingDir: sampleAppRoot);

                Assert.Equal(0, exitCode);
            }
        }
    }
}
