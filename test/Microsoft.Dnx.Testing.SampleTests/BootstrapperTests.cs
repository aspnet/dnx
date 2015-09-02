using Xunit;

namespace Microsoft.Dnx.Testing.SampleTests
{
    public class BootstrapperTests : DnxSdkFunctionalTestBase
    {
        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void BootstrapperInvokesAssemblyWithInferredAppBaseAndLibPath(DnxSdk sdk)
        {
            const string configuration = "Release";
            const string appName = "SimpleConsoleApp";

            var solution = TestUtils.GetSolution(appName, shared: false);
            var project = solution.GetProject(appName);
            var buildOutputPath = project.GetBinPath();

            sdk.Dnu.Restore(project.ProjectDirectory).EnsureSuccess();
            var packOutput = sdk.Dnu.Pack(project.ProjectDirectory, buildOutputPath, configuration: configuration);

            var result = sdk.Dnx.Execute(
                packOutput.GetAssemblyPath(sdk.TargetFramework),
                dnxTraceOn: false);
            result.EnsureSuccess();

            Assert.Equal(@"Hello World!
", result.StandardOutput);
            Assert.Empty(result.StandardError);
        }
    }
}