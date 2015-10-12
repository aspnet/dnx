// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Testing.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    public class DnuPackTests : DnxSdkFunctionalTestBase
    {
        [ConditionalTheory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        public void DnuPack_ClientProfile(DnxSdk sdk)
        {
            const string ProjectName = "ClientProfileProject";
            var projectStructure = new Dir
            {
                [ProjectName] = new Dir
                {
                    ["project.json"] = new JObject
                    {
                        ["frameworks"] = new JObject
                        {
                            ["net40-client"] = new JObject(),
                            ["net35-client"] = new JObject()
                        }
                    }
                },
                ["Output"] = new Dir()
            };

            var baseDir = TestUtils.GetTestFolder<DnuPackTests>(sdk);
            var projectDir = Path.Combine(baseDir, ProjectName);
            var outputDir = Path.Combine(baseDir, "Output");
            projectStructure.Save(baseDir);

            sdk.Dnu.Restore(projectDir).EnsureSuccess();
            var result = sdk.Dnu.Pack(projectDir, outputDir);

            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void CompileModuleWithDeps(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, "CompileModuleWithDependencies");
            var project = solution.GetProject("A");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void P2PDifferentFrameworks(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, "ProjectToProject");
            var project = solution.GetProject("P1");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void AssemblyInfo(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, "AssemblyInfo");
            var project = solution.GetProject("Test");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);

            var assemblyPath = Path.Combine(result.RootPath, "Debug", "dnx451", "Test.dll");
            var assembly = Assembly.LoadFrom(assemblyPath);
            var attributes = assembly.GetCustomAttributes(true);
            Assert.Equal(project.Title, attributes.OfType<AssemblyTitleAttribute>().First().Title);
            Assert.Equal(project.Description, attributes.OfType<AssemblyDescriptionAttribute>().First().Description);
            Assert.Equal(project.Copyright, attributes.OfType<AssemblyCopyrightAttribute>().First().Copyright);
            Assert.Equal(project.AssemblyFileVersion.ToString(), attributes.OfType<AssemblyFileVersionAttribute>().First().Version);
            Assert.Equal(project.Version.ToString(), attributes.OfType<AssemblyInformationalVersionAttribute>().First().InformationalVersion);
            Assert.Equal(project.Version.Version, assembly.GetName().Version);

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
        }
    }
}
