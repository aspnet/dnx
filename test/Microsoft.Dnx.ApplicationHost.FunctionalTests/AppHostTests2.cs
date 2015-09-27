using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Testing;
using Xunit;

namespace Microsoft.Dnx.ApplicationHost.FunctionalTests
{
    public class AppHostTests2 : DnxSdkFunctionalTestBase
    {
        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void LibraryExporterGetExports(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests2>(sdk, "AppHostServicesProjects");
            var project = solution.GetProject("GetExports");

            sdk.Dnu.Restore(project.ProjectDirectory).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Project: {project.Name}", result.StandardOutput);
            Assert.Contains($"Package: Microsoft.Dnx.Compilation.Abstractions", result.StandardOutput);
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void CompileModuleWithDeps(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests2>(sdk, "CompileModuleWithDependencies");
            var project = solution.GetProject("A");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Hello from generated code", result.StandardOutput);
        }

        [Theory]
        [MemberData(nameof(ClrDnxSdks))]
        public void ApplicationWithEcmaEntryPoint(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests2>(sdk, "EcmaEntryPoint");
            var project = solution.GetProject("EcmaEntryPoint");

            sdk.Dnu.Restore(project.ProjectDirectory).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"EntryPoint: Main", result.StandardOutput);
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void RunP2PDifferentFrameworks(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<AppHostTests2>(sdk, "ProjectToProject");
            var project = solution.GetProject("P1");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnx.Execute(project);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("BaseClass.Test()", result.StandardOutput);
            Assert.Contains("Derived.Test", result.StandardOutput);
        }
    }
}
