// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Tooling.FunctionalTests;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Tooling
{
    [Collection(nameof(PackageManagerFunctionalTestCollection))]
    public class DnuRestoreTests
    {
        private readonly PackageManagerFunctionalTestFixture _fixture;

        public DnuRestoreTests(PackageManagerFunctionalTestFixture fixture)
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
        [MemberData(nameof(RuntimeComponents))]
        public void DnuRestore_ExecutesScripts(string flavor, string os, string architecture)
        {
            bool isWindows = TestUtils.CurrentRuntimeEnvironment.OperatingSystem == "Windows";
            var environment = new Dictionary<string, string>
            {
                { "DNX_TRACE", "0" },
            };

            var expectedPreContent =
@"""one""
""two""
"">three""
""four""
";
            var expectedPostContent =
@"""five""
""six""
""argument seven""
""argument eight""
";

            string projectJsonContent;
            string scriptContent;
            string scriptName;
            if (isWindows)
            {
                projectJsonContent =
@"{
  ""frameworks"": {
    ""dnx451"": { }
  },
  ""scripts"": {
    ""prerestore"": [
      ""script.cmd one two > pre"",
      ""script.cmd ^>three >> pre && script.cmd ^ four >> pre""
    ],
    ""postrestore"": [
      ""\""%project:Directory%/script.cmd\"" five six > post"",
      ""\""%project:Directory%/script.cmd\"" \""argument seven\"" \""argument eight\"" >> post""
    ]
  }
}";
                scriptContent =
@"@echo off

:argumentStart
if ""%~1""=="""" goto argumentEnd
echo ""%~1""
shift
goto argumentStart
:argumentEnd";
                scriptName = "script.cmd";
            }
            else
            {
                projectJsonContent =
@"{
  ""frameworks"": {
    ""dnx451"": { }
  },
  ""scripts"": {
    ""prerestore"": [
      ""script one two > pre"",
      ""script.sh \\>three >> pre; ./script.sh four >> pre""
    ],
    ""postrestore"": [
      ""\""%project:Directory%/script\"" five six > post"",
      ""\""%project:Directory%/script.sh\"" \""argument seven\"" \""argument eight\"" >> post""
    ]
  }
}";
                scriptContent =
@"#!/usr/bin/env bash
set -o errexit

for arg in ""$@""; do
  printf ""\""%s\""\n"" ""$arg""
done";
                scriptName = "script.sh";
            }

            var projectStructure =
$@"{{
  '.': ['project.json', '{ scriptName }']
}}";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var testEnv = new DnuTestEnvironment(runtimeHomePath, projectName: "Project Name"))
            {
                var projectPath = testEnv.ProjectPath;
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", projectJsonContent)
                    .WithFileContents(scriptName, scriptContent)
                    .WriteTo(projectPath);
                FileOperationUtils.MarkExecutable(Path.Combine(projectPath, scriptName));

                string output;
                string error;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "restore",
                    arguments: null,
                    stdOut: out output,
                    stdErr: out error,
                    environment: environment,
                    workingDir: projectPath);

                Assert.Equal(0, exitCode);
                Assert.Empty(error);
                Assert.Contains("Executing script 'prerestore' in project.json", output);
                Assert.Contains("Executing script 'postrestore' in project.json", output);

                var preContent = File.ReadAllText(Path.Combine(projectPath, "pre"));
                Assert.Equal(expectedPreContent, preContent);
                var postContent = File.ReadAllText(Path.Combine(projectPath, "post"));
                Assert.Equal(expectedPostContent, postContent);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuRestore_ReinstallsCorruptedPackage(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                var projectDir = Path.Combine(tempDir, "project");
                var packagesDir = Path.Combine(tempDir, "packages");
                var projectJson = Path.Combine(projectDir, Runtime.Project.ProjectFileName);

                Directory.CreateDirectory(projectDir);
                File.WriteAllText(projectJson, @"
{
  ""dependencies"": {
    ""alpha"": ""0.1.0""
  }
}");
                DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "restore",
                    arguments: $"{projectDir} -s {_fixture.PackageSource} --packages {packagesDir}");

                // Corrupt the package by deleting nuspec from it
                var nuspecPath = Path.Combine(packagesDir, "alpha", "0.1.0", $"alpha{Constants.ManifestExtension}");
                File.Delete(nuspecPath);

                string stdOut, stdErr;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "restore",
                    arguments: $"{projectDir} -s {_fixture.PackageSource} --packages {packagesDir}",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
                Assert.Empty(stdErr);
                Assert.Contains($"Installing alpha.0.1.0", stdOut);
                Assert.True(File.Exists(nuspecPath));
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuRestore_ReinstallsPackageWithNormalizedVersion(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                var projectDir = Path.Combine(tempDir, "project");
                var packagesDir = Path.Combine(tempDir, "packages");
                var projectJson = Path.Combine(projectDir, Runtime.Project.ProjectFileName);

                Directory.CreateDirectory(projectDir);
                File.WriteAllText(projectJson, @"
{
  ""dependencies"": {
    ""alpha"": ""0.1.0""
  }
}");
                DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "restore",
                    arguments: $"{projectDir} -s {_fixture.PackageSource} --packages {packagesDir}");

                // rename package folder to an unnormalized string
                Directory.Move(Path.Combine(packagesDir, "alpha", "0.1.0"),
                               Path.Combine(packagesDir, "alpha", "0.1.0.0"));

                // ensure the directory is renamed
                Assert.False(Directory.Exists(Path.Combine(packagesDir, "alpha", "0.1.0")));

                string stdOut, stdErr;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "restore",
                    arguments: $"{projectDir} -s {_fixture.PackageSource} --packages {packagesDir}",
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                Assert.Equal(0, exitCode);
                Assert.Empty(stdErr);
                Assert.Contains($"Installing alpha.0.1.0", stdOut);
                Assert.True(Directory.Exists(Path.Combine(packagesDir, "alpha", "0.1.0")));
                Assert.True(File.Exists(Path.Combine(packagesDir, "alpha", "0.1.0", $"alpha{Constants.ManifestExtension}")));
            }
        }
    }
}
