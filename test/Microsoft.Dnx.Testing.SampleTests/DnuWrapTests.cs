using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.Testing.SampleTests
{
    public class DnuWrapTests : DnxSdkFunctionalTestBase
    {
        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void DnuWrapUpdatesExistingProjectJson(DnxSdk sdk)
        {
            if (RuntimeEnvironmentHelper.IsMono)
            {
                return;
            }

            const string solutionName = "DnuWrapTestProjects";
            var solution = TestUtils.GetSolution(solutionName, shared: false);
            var libraryBetaProject = solution.GetProject("LibraryBeta");

            var expectedProjectJson = new JObject
            {
                ["version"] = "1.0.0-*",
                ["dependencies"] = new JObject { },
                ["frameworks"] = new JObject
                {
                    ["net45+win"] = new JObject
                    {
                        ["wrappedProject"] = "../../LibraryBeta.PCL/LibraryBeta.PCL.csproj",
                        ["bin"] = new JObject
                        {
                            ["assembly"] = "../../LibraryBeta.PCL/obj/{configuration}/LibraryBeta.dll",
                            ["pdb"] = "../../LibraryBeta.PCL/obj/{configuration}/LibraryBeta.pdb"
                        }
                    },
                    ["net45"] = new JObject
                    {
                        ["wrappedProject"] = "../../LibraryBeta.PCL.Desktop/LibraryBeta.PCL.Desktop.csproj",
                        ["bin"] = new JObject
                        {
                            ["assembly"] = "../../LibraryBeta.PCL.Desktop/obj/{configuration}/LibraryBeta.dll",
                            ["pdb"] = "../../LibraryBeta.PCL.Desktop/obj/{configuration}/LibraryBeta.pdb"
                        }
                    }
                }
            };

            var expectedGlobalJson = new JObject
            {
                ["projects"] = new JArray("src", "test")
            };

            sdk.Dnu.Wrap(solution.GetCsprojPath("LibraryBeta.PCL")).EnsureSuccess();
            sdk.Dnu.Wrap(solution.GetCsprojPath("LibraryBeta.PCL.Desktop")).EnsureSuccess();

            // DNX internal JSON writer doesn't follow the indentation convention followed by JSON.NET
            Assert.Equal(expectedGlobalJson.ToString(), JsonUtils.LoadNormalizedJson(solution.GlobalFilePath));
            Assert.False(Directory.Exists(solution.WrapFolderPath));
            Assert.Equal(expectedProjectJson.ToString(), JsonUtils.LoadNormalizedJson(libraryBetaProject.ProjectFilePath));
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void DnuWrapMaintainsAllKindsOfReferences(DnxSdk sdk)
        {
            if (RuntimeEnvironmentHelper.IsMono)
            {
                return;
            }

            const string solutionName = "DnuWrapTestProjects";
            var solution = TestUtils.GetSolution(solutionName, shared: false);

            var expectedLibGammaProjectJson = new JObject
            {
                ["version"] = "1.0.0-*",
                ["frameworks"] = new JObject
                {
                    ["net45"] = new JObject
                    {
                        ["wrappedProject"] = "../../LibraryGamma/LibraryGamma.csproj",
                        ["bin"] = new JObject
                        {
                            ["assembly"] = "../../LibraryGamma/obj/{configuration}/LibraryGamma.dll",
                            ["pdb"] = "../../LibraryGamma/obj/{configuration}/LibraryGamma.pdb"
                        },
                        ["dependencies"] = new JObject
                        {
                            ["EntityFramework"] = "6.1.2-beta1",
                            ["LibraryEpsilon"] = "1.0.0-*",
                            ["LibraryDelta"] = "1.0.0-*",
                        }
                    }
                }
            };

            var expectedLibEpsilonProjectJson = new JObject
            {
                ["version"] = "1.0.0-*",
                ["frameworks"] = new JObject
                {
                    ["net45"] = new JObject
                    {
                        ["wrappedProject"] = "../../LibraryEpsilon/LibraryEpsilon.csproj",
                        ["bin"] = new JObject
                        {
                            ["assembly"] = "../../LibraryEpsilon/obj/{configuration}/LibraryEpsilon.dll",
                            ["pdb"] = "../../LibraryEpsilon/obj/{configuration}/LibraryEpsilon.pdb"
                        }
                    }
                }
            };

            var expectedLibDeltaProjectJson = new JObject
            {
                ["version"] = "1.0.0-*",
                ["frameworks"] = new JObject
                {
                    ["net45"] = new JObject
                    {
                        ["bin"] = new JObject
                        {
                            ["assembly"] = "../../ExternalAssemblies/LibraryDelta.dll"
                        }
                    }
                }
            };

            var expectedGlobalJson = new JObject
            {
                ["projects"] = new JArray("src", "test", "wrap")
            };

            sdk.Dnu.Wrap(solution.GetCsprojPath("LibraryGamma")).EnsureSuccess();

            var libGammaProjectJson = solution.GetProject("LibraryGamma").ProjectFilePath;
            var libEpsilonProjectJson = solution.GetProject("LibraryEpsilon").ProjectFilePath;
            var libDeltaProjectJson = solution.GetProject("LibraryDelta").ProjectFilePath;

            Assert.Equal(expectedGlobalJson.ToString(), JsonUtils.LoadNormalizedJson(solution.GlobalFilePath));
            Assert.Equal(3, Directory.EnumerateDirectories(solution.WrapFolderPath).Count());
            Assert.Equal(expectedLibGammaProjectJson.ToString(), JsonUtils.LoadNormalizedJson(libGammaProjectJson));
            Assert.Equal(expectedLibEpsilonProjectJson.ToString(), JsonUtils.LoadNormalizedJson(libEpsilonProjectJson));
            Assert.Equal(expectedLibGammaProjectJson.ToString(), JsonUtils.LoadNormalizedJson(libGammaProjectJson));
        }
    }
}

