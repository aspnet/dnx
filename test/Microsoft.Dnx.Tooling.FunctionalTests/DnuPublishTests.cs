// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Testing.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    public class DnuPublishTests : DnxSdkFunctionalTestBase
    {
        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void PublishPrunesUnusedTFMsInLockfile(DnxSdk sdk)
        {
            // Arrange
            const string solutionName = "PublishWithDependency";
            const string projectName = "App";
            const string feedName = "A";

            var solution = TestUtils.GetSolution<DnuPublishTests>(sdk, solutionName);
            var project = solution.GetProject(projectName);
            var outputPathFor451 = Path.Combine(solution.RootPath, "Output", "451");
            var outputPathForCore50 = Path.Combine(solution.RootPath, "Output", "core50");
            var feedDir = Path.Combine(solution.SourcePath, feedName);
            var feedOutputPath = Path.Combine(solution.ArtifactsPath, feedName);

            var feedStructure = new Dir(feedDir)
            {
                [Path.Combine("runtimes", "win", "native", "x86"),
                 Path.Combine("lib", "unknown"),
                 Path.Combine("ref", "dotnet")] = new Dir
                {
                    ["A.dll", "A.xml"] = Dir.EmptyFile
                }
            };
            feedStructure.Save(feedDir);

            var expectedOutputStructureFor451 = new Dir
            {
                ["1.0.0"] = new Dir
                {
                    [Path.Combine("ref", "dotnet"),
                     Path.Combine("lib", "unknown"),
                     Path.Combine("lib", "dnx451"),
                     Path.Combine("runtimes", "win", "native", "x86")] = new Dir
                    {
                        ["A.dll"] = Dir.EmptyFile
                    },
                    ["A.nuspec"] = Dir.EmptyFile
                }
            };

            var expectedOutputStructureForCore50 = new Dir
            {
                ["1.0.0"] = new Dir
                {
                    [Path.Combine("ref", "dotnet"),
                     Path.Combine("lib", "unknown"),
                     Path.Combine("lib", "dnxcore50"),
                     Path.Combine("runtimes", "win", "native", "x86")] = new Dir
                    {
                        ["A.dll"] = Dir.EmptyFile
                    },
                    ["A.nuspec"] = Dir.EmptyFile
                }
            };

            // Act
            sdk.Dnu.Restore(feedDir).EnsureSuccess();
            sdk.Dnu.Pack(feedDir, feedOutputPath).EnsureSuccess();
            sdk.Dnu.PackagesAdd(
                Path.Combine(feedOutputPath, "Debug", $"{feedName}.1.0.0.nupkg"),
                solution.LocalPackagesDir).EnsureSuccess();
            Directory.Delete(feedDir, true);

            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectFilePath, outputPathFor451, "--framework dnx451").EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectFilePath, outputPathForCore50, "--framework dnxcore50").EnsureSuccess();
            var actualOutputStructureFor451 = new Dir(Path.Combine(outputPathFor451, "approot", "packages", feedName));
            var actualOutputStructureForCore50 = new Dir(Path.Combine(outputPathForCore50, "approot", "packages", feedName));

            // Assert
            DirAssert.Equal(expectedOutputStructureFor451, actualOutputStructureFor451, compareContents: false);
            DirAssert.Equal(expectedOutputStructureForCore50, actualOutputStructureForCore50, compareContents: false);

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void GlobalJsonInProjectDir(DnxSdk sdk)
        {
            const string solutionName = "GlobalJsonInProjectDir";

            var solution = TestUtils.GetSolution<DnuPublishTests>(sdk, solutionName);
            var projectPath = Path.Combine(solution.RootPath, "Project");
            var outputPath = Path.Combine(solution.RootPath, "Output");

            var expectedOutputGlobalJson = new JObject
            {
                ["sdk"] = new JObject
                {
                    ["version"] = "1.0.0-beta7"
                },
                ["projects"] = new JArray("src"),
                ["packages"] = "packages"
            };
            
            var expectedOutputStructure = new Dir
            {
                ["approot"] = new Dir
                {
                    [Path.Combine("src", "Project")] = new Dir
                    {
                        ["project.json", "project.lock.json"] = new DirItem(Dir.EmptyFile, skipComparison: true)
                    },
                    [$"global.json"] = expectedOutputGlobalJson
                }
            };

            var result = sdk.Dnu.Publish(
                projectPath,
                outputPath);
            result.EnsureSuccess();

            var actualOutputStructure = new Dir(outputPath);
            DirAssert.Equal(expectedOutputStructure, actualOutputStructure);

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppRunsFromSource(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests>(sdk, "HelloWorld");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorld");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectDirectory, outputPath).EnsureSuccess();

            var executable = Path.Combine(outputPath, "approot", "HelloWorld");

            // Assert
            var result = Exec.RunScript(executable, env =>
            {
                env["PATH"] = sdk.BinDir + ";" + Environment.GetEnvironmentVariable("PATH");
            });

            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppRunsNoSource(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests>(sdk, "HelloWorld");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorld");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectDirectory, outputPath, "--no-source").EnsureSuccess();

            var executable = Path.Combine(outputPath, "approot", "HelloWorld");

            // Assert
            var result = Exec.RunScript(executable, env =>
            {
                env["PATH"] = sdk.BinDir + ";" + Environment.GetEnvironmentVariable("PATH");
            });

            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }

        [ConditionalTheory, TraceTest]
        [FrameworkSkipCondition(RuntimeFrameworks.CLR)] //Getting Path is too long exceptions
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppRunsNoSourceAndRT(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests>(sdk, "HelloWorld");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorld");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectDirectory, outputPath, $"--no-source --runtime {sdk.Location}").EnsureSuccess();

            var executable = Path.Combine(outputPath, "approot", "HelloWorld");

            // Assert
            var result = Exec.RunScript(executable, env =>
            {
                env["PATH"] = sdk.BinDir + ";" + Environment.GetEnvironmentVariable("PATH");
            });

            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void IISCommandInvalid(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests>(sdk, "HelloWorldWithWebRoot");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorldWithWebRoot");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            var result = sdk.Dnu.Publish(project.ProjectDirectory, outputPath, $"--iis-command SomethingRandom");

            Assert.Equal(1, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }

        [Theory(Skip = "Failing on the CI due to long path issues."), TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppWithWebRootDefaults(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests>(sdk, "HelloWorldWithWebRoot");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorldWithWebRoot");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectDirectory, outputPath).EnsureSuccess();

            var root = Path.Combine(Path.Combine(outputPath, "approot", "src"), project.Name);
            var json = JObject.Parse(File.ReadAllText(Path.Combine(root, "hosting.json")));
            var config = File.ReadAllText(Path.Combine(root, json?["webroot"]?.ToString(), "web.config"));

            var expected = @"<configuration>
  <system.webServer>
    <handlers>
      <add name=""httpplatformhandler"" path=""*"" verb=""*"" modules=""httpPlatformHandler"" resourceType=""Unspecified"" />
    </handlers>
    <httpPlatform processPath=""..\approot\HelloWorld.cmd"" arguments="""" stdoutLogEnabled=""true"" stdoutLogFile=""..\logs\stdout.log""></httpPlatform>
  </system.webServer>
</configuration>";

            Assert.Equal(RemoveWhitespace(expected), RemoveWhitespace(config));

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }

        private static string RemoveWhitespace(string value)
        {
            return new string(value.ToCharArray().Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        }
    }
}
