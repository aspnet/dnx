// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.FunctionalTestUtils;
using Xunit;

namespace Microsoft.Framework.ApplicationHost
{
    public class KCommandTests
    {
        public static IEnumerable<object[]> KreHomeDirs
        {
            get
            {
                foreach (var path in TestUtils.GetKreHomeDirs())
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KCommandReturnsNonZeroExitCodeWhenNoArgumentWasGiven(DisposableDir kreHomeDir)
        {
            using (kreHomeDir)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    kreHomeDir,
                    subcommand: string.Empty,
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.NotEqual(0, exitCode);
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KCommandReturnsZeroExitCodeWhenHelpOptionWasGiven(DisposableDir kreHomeDir)
        {
            using (kreHomeDir)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    kreHomeDir,
                    subcommand: string.Empty,
                    arguments: "--help",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KCommandShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(DisposableDir kreHomeDir)
        {
            using (kreHomeDir)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    kreHomeDir,
                    subcommand: string.Empty,
                    arguments: "--version",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
                Assert.Contains(TestUtils.GetKreVersion(), stdOut);
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KCommandShowsErrorWhenNoProjectJsonWasFound(DisposableDir kreHomeDir)
        {
            using (kreHomeDir)
            using (var emptyFolder = TestUtils.CreateTempDir())
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    kreHomeDir,
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
        [MemberData("KreHomeDirs")]
        public void KCommandShowsErrorWhenGivenSubcommandWasNotFoundInProjectJson(DisposableDir kreHomeDir)
        {
            var projectStructure = @"{
  'project.json': '{ }'
}";

            using (kreHomeDir)
            using (var projectPath = TestUtils.CreateTempDir())
            {

                DirTree.CreateFromJson(projectStructure).WriteTo(projectPath);

                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    kreHomeDir,
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
