// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Framework.FunctionalTestUtils;
using Xunit;

namespace Microsoft.Framework.ApplicationHost
{
    public class KCommandTests
    {
        public static IEnumerable<object[]> DotnetHomeDirs
        {
            get
            {
                foreach (var path in TestUtils.GetDotnetHomeDirs())
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KCommandReturnsNonZeroExitCodeWhenNoArgumentWasGiven(DisposableDir dotnetHomeDir)
        {
            using (dotnetHomeDir)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    dotnetHomeDir,
                    subcommand: string.Empty,
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.NotEqual(0, exitCode);
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KCommandReturnsZeroExitCodeWhenHelpOptionWasGiven(DisposableDir dotnetHomeDir)
        {
            using (dotnetHomeDir)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    dotnetHomeDir,
                    subcommand: string.Empty,
                    arguments: "--help",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KCommandShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(DisposableDir dotnetHomeDir)
        {
            using (dotnetHomeDir)
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    dotnetHomeDir,
                    subcommand: string.Empty,
                    arguments: "--version",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
                Assert.Contains(TestUtils.GetRuntimeVersion(), stdOut);
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KCommandShowsErrorWhenNoProjectJsonWasFound(DisposableDir dotnetHomeDir)
        {
            using (dotnetHomeDir)
            using (var emptyFolder = TestUtils.CreateTempDir())
            {
                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    dotnetHomeDir,
                    subcommand: "run",
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { "DOTNET_APPBASE", emptyFolder } });

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Unable to locate project.json", stdErr);
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KCommandShowsErrorWhenGivenSubcommandWasNotFoundInProjectJson(DisposableDir dotnetHomeDir)
        {
            var projectStructure = @"{
  'project.json': '{ }'
}";

            using (dotnetHomeDir)
            using (var projectPath = TestUtils.CreateTempDir())
            {

                DirTree.CreateFromJson(projectStructure).WriteTo(projectPath);

                string stdOut, stdErr;
                var exitCode = KCommandTestUtils.ExecKCommand(
                    dotnetHomeDir,
                    subcommand: "invalid",
                    arguments: string.Empty,
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { "DOTNET_APPBASE", projectPath } });

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Unable to load application or execute command 'invalid'.", stdErr);
            }
        }
    }
}
