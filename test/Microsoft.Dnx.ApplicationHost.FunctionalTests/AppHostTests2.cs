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
            var result = sdk.Dnx.Execute($"-p \"{project.ProjectDirectory}\" run");

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Project: {project.Name}", result.StandardOutput);
            Assert.Contains($"Package: Microsoft.Dnx.Compilation.Abstractions", result.StandardOutput);
        }
    }
}
