// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Helpers;
using Microsoft.Dnx.Testing;
using Microsoft.Dnx.Tooling.Publish;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    [Collection(nameof(ToolingFunctionalTestCollection))]
    public class DnuPublishTests2 : DnxSdkFunctionalTestBase
    {
        [ConditionalTheory]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)] // This needs msbuild to run
        [MemberData(nameof(DnxSdks))]
        public void PublishWrappedProjectForSpecificFramework(DnxSdk sdk)
        {
            var solutionName = Path.Combine("DnuWrapTestSolutions", "WrapAndPublish");
            var solution = TestUtils.GetSolution<DnuPublishTests2>(sdk, solutionName);

            // Restore the wrapper project
            sdk.Dnu.Restore(solution.GetWrapperProjectPath("ClassLibrary")).EnsureSuccess();
            sdk.Dnu.Restore(solution.GetProject("DnxConsoleApp")).EnsureSuccess();

            // Build the console app in Debug mode (we specify the config explicitly since dnx here defaults to a Debug 
            // build but on the CI we might have the Configuration environment variable set so that MSBuild builds as Release by default).
            var msbuild = CommonTestUtils.TestUtils.ResolveMSBuildPath();
            Exec.Run(msbuild, "/p:Configuration=Debug", workingDir: Path.Combine(solution.SourcePath, "ClassLibrary")).EnsureSuccess(); ;

            // Publish the console app
            sdk.Dnu.Publish(solution.GetProject("DnxConsoleApp").ProjectFilePath, solution.ArtifactsPath, "--framework dnx451").EnsureSuccess();

            // Get an SDK we can use the RUN the wrapped project (it has to be CLR because the project only supports dnx451)
            var clrSdk = GetRuntime("clr", "win", "x86");

            // Run it
            var outputPath = Path.Combine(solution.ArtifactsPath, "approot", "src", "DnxConsoleApp");
            var result = clrSdk.Dnx.Execute(
                commandLine: $"--project \"{outputPath}\" --configuration Debug DnxConsoleApp run")
                .EnsureSuccess();
            Assert.Contains($"Hello from the wrapped project{Environment.NewLine}", result.StandardOutput);
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void GlobalJsonInProjectDir(DnxSdk sdk)
        {
            const string solutionName = "GlobalJsonInProjectDir";

            var solution = TestUtils.GetSolution<DnuPublishTests>(sdk, solutionName);
            var projectPath = Path.Combine(solution.RootPath, "Project");
            var outputPath = Path.Combine(solution.RootPath, "Output");

            var expectedOutputProjectJson = new JObject
            {
                ["dependencies"] = new JObject { },
                ["frameworks"] = new JObject
                {
                    ["dnx451"] = new JObject { },
                    ["dnxcore50"] = new JObject { }
                }
            };

            var expectedOutputGlobalJson = new JObject
            {
                ["sdk"] = new JObject
                {
                    ["version"] = "1.0.0-beta7"
                },
                ["projects"] = new JArray("src"),
                ["packages"] = "packages"
            };

            var expectedOutputLockFile = new JObject
            {
                ["locked"] = false,
                ["version"] = Constants.LockFileVersion,
                ["targets"] = new JObject
                {
                    ["DNX,Version=v4.5.1"] = new JObject { },
                    ["DNXCore,Version=v5.0"] = new JObject { },
                },
                ["libraries"] = new JObject { },
                ["projectFileDependencyGroups"] = new JObject
                {
                    [""] = new JArray(),
                    ["DNX,Version=v4.5.1"] = new JArray(),
                    ["DNXCore,Version=v5.0"] = new JArray()
                }
            };
            var expectedOutputStructure = new Dir
            {
                ["approot"] = new Dir
                {
                    [$"src/Project"] = new Dir
                    {
                        ["project.json"] = expectedOutputProjectJson,
                        ["project.lock.json"] = expectedOutputLockFile
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
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppRunsFromSource(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests2>(sdk, "HelloWorld");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorld");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectDirectory, outputPath).EnsureSuccess();

            var executable = Path.Combine(outputPath, "HelloWorld");

            // Assert
            var result = Exec.RunScript(executable, env =>
            {
                env["PATH"] = sdk.BinDir + ";" + Environment.GetEnvironmentVariable("PATH");
            });

            Assert.Equal(0, result.ExitCode);
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppRunsNoSource(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests2>(sdk, "HelloWorld");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorld");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectDirectory, outputPath, "--no-source").EnsureSuccess();

            var executable = Path.Combine(outputPath, "HelloWorld");

            // Assert
            var result = Exec.RunScript(executable, env =>
            {
                env["PATH"] = sdk.BinDir + ";" + Environment.GetEnvironmentVariable("PATH");
            });

            Assert.Equal(0, result.ExitCode);
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.CLR)] //Getting Path is too long exceptions
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppRunsNoSourceAndRT(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests2>(sdk, "HelloWorld");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorld");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectDirectory, outputPath, $"--no-source --runtime {sdk.Location}").EnsureSuccess();

            var executable = Path.Combine(outputPath, "HelloWorld");

            // Assert
            var result = Exec.RunScript(executable, env =>
            {
                env["PATH"] = sdk.BinDir + ";" + Environment.GetEnvironmentVariable("PATH");
            });

            Assert.Equal(0, result.ExitCode);
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppWithWebRootFailsIfIISCommandInvalid(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests2>(sdk, "HelloWorldWithWebRoot");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorldWithWebRoot");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            var result = sdk.Dnu.Publish(project.ProjectDirectory, outputPath, $"--iis-command SomethingRandom");

            Assert.NotEqual(0, result.ExitCode);
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void PublishedAppWithWebRootDefaults(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPublishTests2>(sdk, "HelloWorldWithWebRoot");
            var outputPath = Path.Combine(solution.RootPath, "Output");
            var project = solution.GetProject("HelloWorldWithWebRoot");

            // Act
            sdk.Dnu.Restore(project).EnsureSuccess();
            sdk.Dnu.Publish(project.ProjectDirectory, outputPath).EnsureSuccess();

            var config = File.ReadAllText(Path.Combine(outputPath, project.WebRoot, "web.config"));

            var expected = @"<configuration>
  <system.webServer>
    <handlers>
      <add name=""httpplatformhandler"" path=""*"" verb=""*"" modules=""httpPlatformHandler"" resourceType=""Unspecified"" />
    </handlers>
    <httpPlatform processPath=""..\HelloWorld.cmd"" arguments="""" stdoutLogEnabled=""true"" stdoutLogFile=""..\logs\stdout.log""></httpPlatform>
  </system.webServer>
</configuration>";

            Assert.Equal(RemoveWhitespace(expected), RemoveWhitespace(config));
        }

        private static string RemoveWhitespace(string value)
        {
            return new string(value.ToCharArray().Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        }
    }
}
