// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Bootstrapper.FunctionalTests;
using Microsoft.Framework.CommonTestUtils;
using Microsoft.Framework.PackageManager;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.Impl;
using NuGet;
using Xunit;

namespace Microsoft.Framework.ApplicationHost
{
    [Collection("ApplicationHostTestCollection")]
    public class AppHostTests
    {
        private readonly DnxRuntimeFixture _fixture;

        public AppHostTests(DnxRuntimeFixture fixture)
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
        [MemberData("RuntimeComponents")]
        public void AppHostReturnsNonZeroExitCodeWhenNoSubCommandWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: ".",
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.NotEqual(0, exitCode);
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void AppHostReturnsZeroExitCodeWhenHelpOptionWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: ". --help",
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.Equal(0, exitCode);
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void AppHostShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: ". --version",
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.Equal(0, exitCode);
            Assert.Contains(TestUtils.GetRuntimeVersion(), stdOut);
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void AppHostShowsErrorWhenNoProjectJsonWasFound(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var emptyFolder = TestUtils.CreateTempDir())
            {
                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: $"{emptyFolder} run",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Unable to resolve project", stdErr);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void AppHostShowsErrorWhenGivenSubcommandWasNotFoundInProjectJson(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var projectStructure = @"{
  ""project.json"": ""{ }""
}";

            using (var projectPath = TestUtils.CreateTempDir())
            {
                DirTree.CreateFromJson(projectStructure).WriteTo(projectPath);

                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: $"{projectPath} invalid",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.AppBase, projectPath } });

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Unable to load application or execute command 'invalid'.", stdErr);
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void AppHostShowsErrorWhenCurrentTargetFrameworkWasNotFoundInProjectJson(string flavor, string os, string architecture)
        {
            var runtimeTargetFrameworkString = flavor == "coreclr" ? FrameworkNames.LongNames.DnxCore50 : FrameworkNames.LongNames.Dnx451;
            var runtimeTargetFramework = new FrameworkName(runtimeTargetFrameworkString);
            var runtimeTargetFrameworkShortName = VersionUtility.GetShortFrameworkName(runtimeTargetFramework);
            var runtimeType = flavor == "coreclr" ? "CoreCLR" : "CLR";
            runtimeType = PlatformHelper.IsMono ? "Mono" : runtimeType;
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            var projectJsonContents = @"{
  ""frameworks"": {
    ""FRAMEWORK_NAME"": { }
  }
}".Replace("FRAMEWORK_NAME", flavor == "coreclr" ? "dnx451" : "dnxcore50");

            using (runtimeHomeDir)
            using (var projectPath = new DisposableDir())
            {
                var projectName = new DirectoryInfo(projectPath).Name;
                var projectJsonPath = Path.Combine(projectPath, Project.ProjectFileName);
                File.WriteAllText(projectJsonPath, projectJsonContents);

                string stdOut, stdErr;
                var exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: $"{projectPath} run",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                var expectedErrorMsg =$@"The current runtime target framework is not compatible with '{projectName}'.

Current runtime Target Framework: '{runtimeTargetFramework} ({runtimeTargetFrameworkShortName})'
  Type: {runtimeType}
  Architecture: {architecture ?? TestUtils.CurrentRuntimeEnvironment.RuntimeArchitecture}
  Version: {TestUtils.GetRuntimeVersion()}

Please make sure the runtime matches a framework specified in {Project.ProjectFileName}";

                Assert.NotEqual(0, exitCode);
                Assert.Contains(expectedErrorMsg, stdErr);
            }
        }

        public static IEnumerable<object[]> ClrRuntimeComponentsAndCommandsSet
        {
            get
            {
                // command name -> expected output
                var commands = new Dictionary<string, string>
                {
                    {
                        "one",
@"0: 'one'
1: 'two'
2: 'extra'
"
                    },
                    {
                        "two",
@"0: '^>three'
1: '&&>>^""'
2: 'extra'
"
                    },
                    {
                        "three",
@"0: 'four'
1: 'argument five'
2: 'extra'
"
                    },
                    {
                        "run",
@"0: 'extra'
"
                    },
                };

                var data = new TheoryData<object, object, object, string, string>();
                foreach (var component in TestUtils.GetClrRuntimeComponents())
                {
                    foreach (var command in commands)
                    {
                        data.Add(component[0], component[1], component[2], command.Key, command.Value);
                    }
                }

                return data;
            }
        }

        [Theory]
        [MemberData(nameof(ClrRuntimeComponentsAndCommandsSet))]
        public void AppHost_ExecutesCommands(
            string flavor,
            string os,
            string architecture,
            string command,
            string expectedOutput)
        {
            var environment = new Dictionary<string, string>
            {
                { "DNX_TRACE", "0" },
            };

            var projectName = "Project Name";
            var projectStructure =
$@"{{
  '.': ['Program.cs', '{ Project.ProjectFileName }']
}}";
            var programContents =
@"using System;

namespace Project_Name
{
    public class Program
    {
        public void Main(string[] arguments)
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                var argument = arguments[i];
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    Console.WriteLine($""{ i }: '{ argument }'"");
                }
            }
        }
    }
}";
            var projectJsonContents =
$@"{{
  ""commands"": {{
    ""one"": ""\""{ projectName }\"" one two"",
    ""two"": ""\""{ projectName }\"" ^>three &&>>^\"""",
    ""three"": ""\""{ projectName }\"" four \""argument five\""""
  }},
  ""frameworks"" : {{
    ""dnx451"": {{ }}
  }}
}}";

            using (var applicationRoot = TestUtils.CreateTempDir())
            {
                var projectPath = Path.Combine(applicationRoot, projectName);
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("Program.cs", programContents)
                    .WithFileContents(Project.ProjectFileName, projectJsonContents)
                    .WriteTo(projectPath);
                var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "restore",
                    arguments: null,
                    environment: environment,
                    workingDir: projectPath);
                Assert.Equal(0, exitCode); // Guard

                string output;
                string error;
                exitCode = BootstrapperTestUtils.ExecBootstrapper(
                    runtimeHomePath,
                    arguments: $@"""{ projectPath }"" { command } extra",
                    stdOut: out output,
                    stdErr: out error,
                    environment: environment);

                Assert.Equal(0, exitCode);
                Assert.Empty(error);
                Assert.Equal(expectedOutput, output);
            }
        }
    }
}
