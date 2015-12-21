using System.IO;
using System.Linq;
using Microsoft.Dnx.Testing.Framework;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace Microsoft.Dnx.ApplicationHost.FunctionalTests
{
    public class DllImportTests : DnxSdkFunctionalTestBase
    {
        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void VerifyDllImportWithPackageReference(DnxSdk sdk)
        {
            if (ShouldSkipTest)
            {
                return;
            }

            var solution = TestUtils.GetSolution<DllImportTests>(sdk, "DllImportTestProjects");
            var project = solution.GetProject("PackageReferenceTest");
            var artifactsPath = Path.Combine(solution.ArtifactsPath, "NativeLib");
            sdk.Dnu.Restore(solution.GetProject("NativeLib")).EnsureSuccess();
            sdk.Dnu.Pack(
                solution.GetProject("NativeLib").ProjectDirectory,
                artifactsPath,
                configuration: "package").EnsureSuccess();

            sdk.Dnu.Restore(solution.RootPath, packagesDir: null, feeds: null, additionalArguments: "-f " + Path.Combine(artifactsPath, "package"))
                .EnsureSuccess();

            var result = sdk.Dnx.Execute(project, dnxTraceOn: true);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("The answer is 42", result.StandardOutput);

            if (sdk.Flavor == "coreclr")
            {
                Assert.Contains("Information: [PackageAssemblyLoader]: Loaded unmanaged library=nativelib", result.StandardOutput);
            }
            else
            {
                var lines = result.StandardOutput.Split('\n');

                if (sdk.Flavor == "clr")
                {
                    Assert.True(lines.Any(l =>
                        l.StartsWith("Information: [PackageDependencyProvider]: Enabling loading native libraries from packages by extendig %PATH% with")
                        && l.Contains("NativeLib")));
                }
                else
                {
                    Assert.True(lines.Any(l =>
                        l.StartsWith("Information: [PackageDependencyProvider]: Preloading:")
                        && l.Contains("nativelib")
                        && l.Contains("succeeded")));
                }
            }

            TestUtils.CleanUpTestDir<DllImportTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void VerifyDllImportWithProjectReference(DnxSdk sdk)
        {
            if (ShouldSkipTest)
            {
                return;
            }

            var solution = TestUtils.GetSolution<DllImportTests>(sdk, "DllImportTestProjects");
            var project = solution.GetProject("ProjectReferenceTest");
            sdk.Dnu.Restore(project).EnsureSuccess();
            var result = sdk.Dnx.Execute(project, dnxTraceOn: true);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"The answer is 42", result.StandardOutput);

            if (sdk.Flavor == "coreclr")
            {
                Assert.Contains("Information: [ProjectAssemblyLoader]: Loaded unmanaged library=nativelib", result.StandardOutput);
            }
            else
            {
                var lines = result.StandardOutput.Split('\n');

                if (sdk.Flavor == "clr")
                {
                    Assert.True(lines.Any(l =>
                        l.StartsWith("Information: [PackageDependencyProvider]: Enabling loading native libraries from projects by extendig %PATH% with")
                        && l.Contains("NativeLib")));
                }
                else
                {
                    Assert.True(lines.Any(l =>
                        l.StartsWith("Information: [PackageDependencyProvider]: Preloading:")
                        && l.Contains("nativelib")
                        && l.Contains("succeeded")));
                }
            }

            TestUtils.CleanUpTestDir<DllImportTests>(sdk);
        }

        private static bool ShouldSkipTest
        {
            get
            {
                var runtimeEnvironment = PlatformServices.Default.Runtime;

                // Preloading .so's on Linux does not work and setting
                // LD_LIBRARY_PATH is not an option so skipping
                return runtimeEnvironment.OperatingSystem == "ubuntu" && runtimeEnvironment.RuntimeType == "Mono";
            }
        }
    }
}
