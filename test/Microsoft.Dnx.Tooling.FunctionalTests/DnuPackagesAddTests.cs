// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Newtonsoft.Json.Linq;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Tooling
{
    public class DnuPackagesAddTests
    {
        private static readonly string ProjectName = "HelloWorld";
        private static readonly SemanticVersion ProjectVersion = new SemanticVersion("0.1-beta");
        private static readonly string Configuration = "Release";
        private static readonly string PackagesDirName = "packages";
        private static readonly string OutputDirName = "output";

        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                return TestUtils.GetRuntimeComponentsCombinations();
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPackagesAddSkipsInstalledPackageWhenShasMatch(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempSamplesDir = TestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            {
                var projectFilePath = Path.Combine(tempSamplesDir, ProjectName, Runtime.Project.ProjectFileName);
                var packagesDir = Path.Combine(tempSamplesDir, PackagesDirName);
                var packagePathResolver = new DefaultPackagePathResolver(packagesDir);
                var nuspecPath = packagePathResolver.GetManifestFilePath(ProjectName, ProjectVersion);

                BuildPackage(tempSamplesDir, runtimeHomeDir);

                string stdOut;
                var exitCode = DnuPackagesAddOutputPackage(tempSamplesDir, runtimeHomeDir, out stdOut);
                Assert.Equal(0, exitCode);
                // possible target for PR
                Assert.Contains($"Installing {ProjectName}.{ProjectVersion}", stdOut);

                var lastInstallTime = new FileInfo(nuspecPath).LastWriteTimeUtc;

                exitCode = DnuPackagesAddOutputPackage(tempSamplesDir, runtimeHomeDir, out stdOut);
                Assert.Equal(0, exitCode);
                Assert.Contains($"{ProjectName}.{ProjectVersion} already exists", stdOut);
                Assert.Equal(lastInstallTime, new FileInfo(nuspecPath).LastWriteTimeUtc);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPackagesAddOverwritesInstalledPackageWhenShasDoNotMatch(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempSamplesDir = TestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            {
                var projectFilePath = Path.Combine(tempSamplesDir, ProjectName, Runtime.Project.ProjectFileName);
                var packagesDir = Path.Combine(tempSamplesDir, PackagesDirName);
                var packagePathResolver = new DefaultPackagePathResolver(packagesDir);
                var nuspecPath = packagePathResolver.GetManifestFilePath(ProjectName, ProjectVersion);

                SetProjectDescription(projectFilePath, "Old");
                BuildPackage(tempSamplesDir, runtimeHomeDir);

                string stdOut;
                var exitCode = DnuPackagesAddOutputPackage(tempSamplesDir, runtimeHomeDir, out stdOut);
                Assert.Equal(0, exitCode);
                // possible target for PR
                Assert.Contains($"Installing {ProjectName}.{ProjectVersion}", stdOut);

                var lastInstallTime = new FileInfo(nuspecPath).LastWriteTimeUtc;

                SetProjectDescription(projectFilePath, "New");
                BuildPackage(tempSamplesDir, runtimeHomeDir);

                exitCode = DnuPackagesAddOutputPackage(tempSamplesDir, runtimeHomeDir, out stdOut);
                Assert.Equal(0, exitCode);
                Assert.Contains($"Overwriting {ProjectName}.{ProjectVersion}", stdOut);

                var xDoc = XDocument.Load(packagePathResolver.GetManifestFilePath(ProjectName, ProjectVersion));
                var actualDescription = xDoc.Root.Descendants()
                    .Single(x => string.Equals(x.Name.LocalName, "description")).Value;
                Assert.Equal("New", actualDescription);
                Assert.NotEqual(lastInstallTime, new FileInfo(nuspecPath).LastWriteTimeUtc);
            }
        }

        private static void SetProjectDescription(string projectFilePath, string description)
        {
            var json = JObject.Parse(File.ReadAllText(projectFilePath));
            json["description"] = description;
            File.WriteAllText(projectFilePath, json.ToString());
        }

        private static void BuildPackage(string sampleDir, string runtimeHomeDir)
        {
            var projectDir = Path.Combine(sampleDir, ProjectName);
            var buildOutpuDir = Path.Combine(sampleDir, OutputDirName);
            int exitCode = DnuTestUtils.ExecDnu(
                runtimeHomeDir,
                "pack",
                $"{projectDir} --out {buildOutpuDir} --configuration {Configuration}",
                environment: new Dictionary<string, string> { { "DNX_BUILD_VERSION", null } });
            Assert.Equal(0, exitCode);
        }

        private static int DnuPackagesAddOutputPackage(string sampleDir, string runtimeHomeDir, out string stdOut)
        {
            var packagePath = Path.Combine(sampleDir, OutputDirName, Configuration,
                $"{ProjectName}.{ProjectVersion}{NuGet.Constants.PackageExtension}");
            var packagesDir = Path.Combine(sampleDir, PackagesDirName);

            string stdErr;
            var exitCode = DnuTestUtils.ExecDnu(
                runtimeHomeDir,
                "packages",
                $"add {packagePath} {packagesDir}",
                out stdOut,
                out stdErr);

            return exitCode;
        }
    }
}
