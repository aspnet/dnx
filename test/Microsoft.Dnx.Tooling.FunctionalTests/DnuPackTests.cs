using System.IO;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Testing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    public partial class DnuPackTests : DnxSdkFunctionalTestBase
    {
        [ConditionalTheory]
        [MemberData(nameof(DnxSdks))]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        public void DnuPack_ClientProfile(DnxSdk sdk)
        {
            const string ProjectName = "ClientProfileProject";
            var projectStructure = new Dir
            {
                [ProjectName] = new Dir
                {
                    ["project.json"] = new JObject
                    {
                        ["frameworks"] = new JObject
                        {
                            ["net40-client"] = new JObject(),
                            ["net35-client"] = new JObject()
                        }
                    }
                },
                ["Output"] = new Dir()
            };

            var baseDir = TestUtils.GetTestFolder<DnuPackTests>(sdk);
            var projectDir = Path.Combine(baseDir, ProjectName);
            var outputDir = Path.Combine(baseDir, "Output");
            projectStructure.Save(baseDir);

            sdk.Dnu.Restore(projectDir).EnsureSuccess();
            var result = sdk.Dnu.Pack(projectDir, outputDir);

            Assert.Equal(0, result.ExitCode);
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void CompileModuleWithDeps(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, "CompileModuleWithDependencies");
            var project = solution.GetProject("A");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void P2PDifferentFrameworks(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, "ProjectToProject");
            var project = solution.GetProject("P1");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);
        }
    }
}
