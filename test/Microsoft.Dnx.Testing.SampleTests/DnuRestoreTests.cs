using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Dnx.Testing.SampleTests
{
    [Collection("SampleTestCollection")]
    public class DnuRestoreTests : DnxSdkFunctionalTestBase
    {
        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void DnuRestoreInstallsIndirectDependency(DnxSdk sdk)
        {
            // SimpleChain -> DependencyA -> DependencyB
            const string feedSolutionName = "DependencyGraphsFeed";
            const string projectSolutionName = "DependencyGraphsProject";
            const string projectName = "SimpleChain";

            var feedSolution = TestUtils.GetSolution<DnuRestoreTests>(sdk, feedSolutionName, appendSolutionNameToTestFolder: true);
            var localFeed = TestUtils.CreateLocalFeed<DnuRestoreTests>(sdk, feedSolution);

            var projectSolution = TestUtils.GetSolution<DnuRestoreTests>(sdk, projectSolutionName, appendSolutionNameToTestFolder: true);
            var project = projectSolution.GetProject(projectName);
            var packagesDir = Path.Combine(project.ProjectDirectory, "packages");

            var result = sdk.Dnu.Restore(
                project.ProjectDirectory,
                packagesDir,
                feeds: new [] { localFeed });
            result.EnsureSuccess();

            Assert.Empty(result.StandardError);
            Assert.Contains($"Installing DependencyA.1.0.0", result.StandardOutput);
            Assert.Contains($"Installing DependencyB.2.0.0", result.StandardOutput);
            Assert.True(Directory.Exists(Path.Combine(packagesDir, "DependencyA", "1.0.0")));
            Assert.True(Directory.Exists(Path.Combine(packagesDir, "DependencyB", "2.0.0")));
        }
    }
}
