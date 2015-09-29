// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Testing.Framework;
using Xunit;

namespace Bootstrapper.FunctionalTests
{
    [Collection(nameof(BootstrapperTestCollection))]
    public class BootstrapperTests : DnxSdkFunctionalTestBase
    {
        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void UnknownCommandDoesNotThrow(DnxSdk sdk)
        {
            const string solutionName = "BootstrapperSolution";
            const string projectName = "TesterProgram";
            const string unknownCommand = "unknownCommand";
            const string testerCommand = "TesterProgram";
            var solution = TestUtils.GetSolution<BootstrapperTests>(sdk, solutionName);
            var project = solution.GetProject(projectName);

            var result = sdk.Dnx.Execute($"--project {project.ProjectDirectory} {unknownCommand}");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal($"Error: Unable to load application or execute command '{unknownCommand}'. Available commands: {testerCommand}.{Environment.NewLine}", result.StandardError);

            TestUtils.CleanUpTestDir<BootstrapperTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void UnresolvedProjectDoesNotThrow(DnxSdk sdk)
        {
            const string solutionName = "BootstrapperSolution";
            var solution = TestUtils.GetSolution<BootstrapperTests>(sdk, solutionName);

            var result = sdk.Dnx.Execute($"--project {solution.RootPath} run");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal($"Error: Unable to resolve project from {solution.RootPath}{Environment.NewLine}", result.StandardError);

            TestUtils.CleanUpTestDir<BootstrapperTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void UserExceptionsThrows(DnxSdk sdk)
        {
            const string solutionName = "BootstrapperSolution";
            const string projectName = "TesterProgram";
            var solution = TestUtils.GetSolution<BootstrapperTests>(sdk, solutionName);
            var project = solution.GetProject(projectName);

            sdk.Dnu.Restore(project).EnsureSuccess();
            var result = sdk.Dnx.Execute($"--project {project.ProjectDirectory} run");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("System.Exception: foo", result.StandardError);

            TestUtils.CleanUpTestDir<BootstrapperTests>(sdk);
        }
    }
}
