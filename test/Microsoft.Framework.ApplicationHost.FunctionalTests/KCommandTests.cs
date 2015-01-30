// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.FunctionalTestUtils;
using Microsoft.Framework.Runtime;
using Xunit;

namespace Microsoft.Framework.ApplicationHost
{
    public class KCommandTests
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
        public void KCommandReturnsNonZeroExitCodeWhenNoArgumentWasGiven(DisposableDir runtimeHomeDir)
        {
            using (runtimeHomeDir)
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
        [MemberData("RuntimeHomeDirs")]
        public void KCommandReturnsZeroExitCodeWhenHelpOptionWasGiven(DisposableDir runtimeHomeDir)
        {
            using (runtimeHomeDir)
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
        [MemberData("RuntimeHomeDirs")]
        public void KCommandShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(DisposableDir runtimeHomeDir)
        {
            using (runtimeHomeDir)
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
        [MemberData("RuntimeHomeDirs")]
        public void KCommandShowsErrorWhenNoProjectJsonWasFound(DisposableDir runtimeHomeDir)
        {
            using (runtimeHomeDir)
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
                Assert.Contains("Unable to locate project.json", stdErr);
            }
        }

        [Theory]
        [MemberData("RuntimeHomeDirs")]
        public void KCommandShowsErrorWhenGivenSubcommandWasNotFoundInProjectJson(DisposableDir runtimeHomeDir)
        {
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
    }
}
