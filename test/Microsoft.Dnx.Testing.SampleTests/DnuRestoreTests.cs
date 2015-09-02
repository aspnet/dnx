using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Dnx.Testing.SampleTests
{
    public class DnuRestoreTests : DnxSdkFunctionalTestBase
    {
        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void DnuRestoreInstallsIndirectDependency(DnxSdk sdk)
        {
            // SimpleChain -> DependencyA -> DependencyB
            const string appName = "SimpleChain";
            const string solutionName = "DependencyGraphs";

            var solution = TestUtils.GetSolution(solutionName, shared: true);
            var project = solution.GetProject(appName);
            var localFeed = TestUtils.CreateLocalFeed(solution);
            var tempDir = TestUtils.GetLocalTempFolder();
            var packagesDir = Path.Combine(tempDir, "packages");
            var projectDir = Path.Combine(tempDir, project.Name);
            TestUtils.CopyFolder(project.ProjectDirectory, projectDir);

            var result = sdk.Dnu.Restore(
                projectDir,
                packagesDir,
                feeds: new string[] { localFeed });
            result.EnsureSuccess();

            Assert.Empty(result.StandardError);
            Assert.Contains($"Installing DependencyA.1.0.0", result.StandardOutput);
            Assert.Contains($"Installing DependencyB.2.0.0", result.StandardOutput);
            Assert.Equal(2, Directory.EnumerateFileSystemEntries(packagesDir).Count());
            Assert.True(Directory.Exists(Path.Combine(packagesDir, "DependencyA", "1.0.0")));
            Assert.True(Directory.Exists(Path.Combine(packagesDir, "DependencyB", "2.0.0")));
        }
    }
}
