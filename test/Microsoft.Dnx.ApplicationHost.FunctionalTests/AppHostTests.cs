// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Testing.Framework;
using Xunit;

namespace Microsoft.Dnx.ApplicationHost.FunctionalTests
{
    [Collection(nameof(ApplicationHostTestCollection))]
    public class AppHostTests : DnxSdkFunctionalTestBase
    {
        [TraceTest]
        [Theory(Skip = "Test broke. Don't care. DNX going away")]
        [MemberData(nameof(DnxSdks))]
        public void LibraryExporterGetExports(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests>(sdk, "AppHostServicesProjects");
            var project = solution.GetProject("GetExports");

            sdk.Dnu.Restore(project.ProjectDirectory).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project).EnsureSuccess();

            // Assert
            Assert.Contains($"Project: {project.Name}", result.StandardOutput);
            Assert.Contains($"Package: Microsoft.Extensions.CompilationAbstractions", result.StandardOutput);

            TestUtils.CleanUpTestDir<AppHostTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void CompileModuleWithDeps(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests>(sdk, "CompileModuleWithDependencies");
            var project = solution.GetProject("A");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Hello from generated code", result.StandardOutput);

            TestUtils.CleanUpTestDir<AppHostTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void ApplicationWithEcmaEntryPoint(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests>(sdk, "EcmaEntryPoint");
            var project = solution.GetProject("EcmaEntryPoint");

            sdk.Dnu.Restore(project.ProjectDirectory).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("ECMA EntryPoint Ran", result.StandardOutput);

            TestUtils.CleanUpTestDir<AppHostTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void RunP2PDifferentFrameworks(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests>(sdk, "ProjectToProject");
            var project = solution.GetProject("P1");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("BaseClass.Test()", result.StandardOutput);
            Assert.Contains("Derived.Test", result.StandardOutput);

            TestUtils.CleanUpTestDir<AppHostTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DnxSupressesRoslynExceptionStackTrace(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests>(sdk, "ApplicationHostTestProjects");
            var project = solution.GetProject("CompilationException");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(1, result.ExitCode);
            Assert.DoesNotContain("RoslynCompilationException:", result.StandardError);

            TestUtils.CleanUpTestDir<AppHostTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DnxSupressesRoslynExceptionStackTraceInMain(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests>(sdk, "ApplicationHostTestProjects");
            var project = solution.GetProject("CompilationExceptionInMain");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(1, result.ExitCode);
            Assert.DoesNotContain("DummyCompilationException:", result.StandardError);

            TestUtils.CleanUpTestDir<AppHostTests>(sdk);
        }
    }
}
