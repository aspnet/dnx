// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Testing.Framework;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    // This needs msbuild to run
    public class DnuPublishWrappedTests : DnxSdkFunctionalTestBase
    {
        [ConditionalTheory, TraceTest]
        [OSSkipCondition(OperatingSystems.Linux)]
        [OSSkipCondition(OperatingSystems.MacOSX)]
        [MemberData(nameof(DnxSdks))]
        public void PublishWrappedProjectForSpecificFramework(DnxSdk sdk)
        {
            DoPublish(sdk, "--framework dnx451");
        }

        [ConditionalTheory, TraceTest]
        [OSSkipCondition(OperatingSystems.Linux)]
        [OSSkipCondition(OperatingSystems.MacOSX)]
        [MemberData(nameof(DnxSdks))]
        public void PublishWrappedProject(DnxSdk sdk)
        {
            DoPublish(sdk);
        }

        [ConditionalTheory, TraceTest]
        [OSSkipCondition(OperatingSystems.Linux)]
        [OSSkipCondition(OperatingSystems.MacOSX)]
        [MemberData(nameof(ClrDnxSdks))] // project only supports dnx451
        public void PublishWrappedProjectForSpecificRuntime(DnxSdk sdk)
        {
            DoPublish(sdk, $"--runtime \"{sdk.Location}\"");
        }

        private static void DoPublish(DnxSdk sdk, string arguments = null)
        {
            var solutionName = Path.Combine("DnuWrapTestSolutions", "WrapAndPublish");
            var solution = TestUtils.GetSolution<DnuPublishWrappedTests>(sdk, solutionName);

            // Restore the wrapper project
            sdk.Dnu.Restore(solution.GetWrapperProjectPath("ClassLibrary")).EnsureSuccess();
            sdk.Dnu.Restore(solution.GetProject("DnxConsoleApp")).EnsureSuccess();

            // Build the console app in Debug mode (we specify the config explicitly since dnx here defaults to a Debug
            // build but on the CI we might have the Configuration environment variable set so that MSBuild builds as Release by default).
            var msbuild = CommonTestUtils.TestUtils.ResolveMSBuildPath();
            Exec.Run(msbuild, "/p:Configuration=Debug", workingDir: Path.Combine(solution.SourcePath, "ClassLibrary"))
                .EnsureSuccess();

            // Publish the console app
            var publishResult = sdk.Dnu.Publish(solution.GetProject("DnxConsoleApp").ProjectFilePath, solution.ArtifactsPath, arguments);
            publishResult.EnsureSuccess();

            // Get an SDK we can use the RUN the wrapped project (it has to be CLR because the project only supports dnx451)
            var clrSdk = GetRuntime("clr", "win", "x86");

            // Run it
            var outputPath = Path.Combine(solution.ArtifactsPath, "approot", "src", "DnxConsoleApp");
            var result = clrSdk.Dnx.Execute(
                commandLine: $"--project \"{outputPath}\" --configuration Debug DnxConsoleApp run")
                .EnsureSuccess();
            Assert.Contains($"Hello from the wrapped project{Environment.NewLine}", result.StandardOutput);

            TestUtils.CleanUpTestDir<DnuPublishWrappedTests>(sdk);
        }
    }
}