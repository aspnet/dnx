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
    public class DnuInstallTests
    {
        private readonly PackageManagerFunctionalTestFixture _fixture;

        public DnuInstallTests(PackageManagerFunctionalTestFixture fixture)
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
        public void DnuInstall_WithProjectPathArgument(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                var packagesDir = Path.Combine(tempDir, "packages");
                var projectDir = Path.Combine(tempDir, "project");
                Directory.CreateDirectory(projectDir);
                var projectJsonPath = Path.Combine(projectDir, Runtime.Project.ProjectFileName);
                File.WriteAllText(projectJsonPath, @"{
  ""dependencies"": { }
}");

                VerifyDnuInstall(
                    runtimeHomePath,
                    packageName: "alpha",
                    packageVersion: "0.1.0",
                    projectDir: projectDir,
                    packagesDir: packagesDir,
                    workingDir: null);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuInstall_WithoutProjectPathArgument(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                var packagesDir = Path.Combine(tempDir, "packages");
                var projectDir = Path.Combine(tempDir, "project");
                Directory.CreateDirectory(projectDir);
                var projectJsonPath = Path.Combine(projectDir, Runtime.Project.ProjectFileName);
                File.WriteAllText(projectJsonPath, @"{
  ""dependencies"": { }
}");

                VerifyDnuInstall(
                    runtimeHomePath,
                    packageName: "alpha",
                    packageVersion: "0.1.0",
                    projectDir: null,
                    packagesDir: packagesDir,
                    workingDir: projectDir);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuInstall_OverwriteOldVersion(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                var packagesDir = Path.Combine(tempDir, "packages");
                var projectDir = Path.Combine(tempDir, "project");
                Directory.CreateDirectory(projectDir);
                var projectJsonPath = Path.Combine(projectDir, Runtime.Project.ProjectFileName);
                File.WriteAllText(projectJsonPath, @"{
  ""dependencies"": {
    ""alpha"": ""0.0.0""
  }
}");

                VerifyDnuInstall(
                    runtimeHomePath,
                    packageName: "alpha",
                    packageVersion: "0.1.0",
                    projectDir: projectDir,
                    packagesDir: packagesDir,
                    workingDir: projectDir);
            }
        }

        private void VerifyDnuInstall(
            string runtimeHomePath,
            string packageName,
            string packageVersion,
            string projectDir,
            string packagesDir,
            string workingDir)
        {
            var projectJsonPath = Path.Combine(projectDir ?? workingDir, Runtime.Project.ProjectFileName);
            var expectedProjectJson = $@"{{
  ""dependencies"": {{
    ""{packageName}"": ""{packageVersion}""
  }}
}}";

            string stdOut, stdErr;
            var exitCode = DnuTestUtils.ExecDnu(
                runtimeHomePath,
                subcommand: "install",
                arguments: $"{packageName} {packageVersion} {projectDir} -s {_fixture.PackageSource} --packages {packagesDir}",
                stdOut: out stdOut,
                stdErr: out stdErr,
                environment: null,
                workingDir: workingDir);

            Assert.Equal(0, exitCode);
            Assert.Empty(stdErr);
            // possible target for PR
            Assert.Contains($"Installing {packageName}.{packageVersion}", stdOut);
            Assert.Equal(expectedProjectJson, File.ReadAllText(projectJsonPath));
            Assert.True(Directory.Exists(Path.Combine(packagesDir, packageName, packageVersion)));
        }
    }
}
