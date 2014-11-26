// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.FunctionalTestUtils;
using Xunit;

namespace Microsoft.Framework.ApplicationHost
{
    public class KCommandTests
    {
        public static IEnumerable<object[]> KrePaths
        {
            get
            {
                foreach (var path in TestUtils.GetUnpackedKrePaths())
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KCommandReturnsNonZeroExitCodeWhenNoArgumentWasGiven(DisposableDirPath krePath)
        {
            using (krePath)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    krePath,
                    subcommand: string.Empty,
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.NotEqual(0, exitCode);
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KCommandReturnsZeroExitCodeWhenHelpOptionWasGiven(DisposableDirPath krePath)
        {
            using (krePath)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    krePath,
                    subcommand: string.Empty,
                    arguments: "--help",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KSlashQuestionMarkDoesNotShowHelpInformationOfCmd(DisposableDirPath krePath)
        {
            using (krePath)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    krePath,
                    subcommand: string.Empty,
                    arguments: "/?",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                // If "k /?" shows help information of cmd.exe, the exit code should be 0
                Assert.NotEqual(0, exitCode);
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KCommandShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(DisposableDirPath krePath)
        {
            using (krePath)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    krePath,
                    subcommand: string.Empty,
                    arguments: "--version",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
                Assert.Contains(TestUtils.GetKreVersion(), stdOut);
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KCommandShowsErrorWhenNoProjectJsonWasFound(DisposableDirPath krePath)
        {
            using (krePath)
            using (var emptyFolder = TestUtils.CreateTempDir())
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    krePath,
                    subcommand: "run",
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { "K_APPBASE", emptyFolder } });

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Unable to locate project.json", stdErr);
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KCommandShowsErrorWhenGivenSubcommandWasNotFoundInProjectJson(DisposableDirPath krePath)
        {
            var projectStructure = @"{
  'project.json': '{ }'
}";

            using (krePath)
            using (var projectPath = TestUtils.CreateTempDir())
            {

                TestUtils.CreateDirTree(projectStructure).WriteTo(projectPath);

                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    krePath,
                    subcommand: "invalid",
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { "K_APPBASE", projectPath } });

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Unable to load application or execute command 'invalid'.", stdErr);
            }
        }
    }
}
