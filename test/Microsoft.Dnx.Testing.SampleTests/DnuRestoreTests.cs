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
            const string appName = "SimpleChain";
            const string solutionName = "DependencyGraphs";

            var solution = TestUtils.GetSolution<DnuRestoreTests>(sdk, solutionName);
            var project = solution.GetProject(appName);
            var tempProjectDir = TestUtils.GetTempTestFolder<DnuRestoreTests>(sdk);
            TestUtils.CopyFolder(project.ProjectDirectory, tempProjectDir);
            var localFeed = TestUtils.CreateLocalFeed<DnuRestoreTests>(sdk, solution);
            var packagesDir = Path.Combine(tempProjectDir, "packages");

            var result = sdk.Dnu.Restore(
                tempProjectDir,
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
