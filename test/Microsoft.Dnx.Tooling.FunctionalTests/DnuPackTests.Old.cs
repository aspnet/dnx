// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.CommonTestUtils;
using NuGet;
using Xunit;
using TestUtils = Microsoft.Dnx.CommonTestUtils.TestUtils;

namespace Microsoft.Dnx.Tooling.FunctionalTest.Old
{
    // DO NOT add further tests to this class! Add them to the 'DnuPackTests.cs' file.
    public partial class DnuPackTests
    {
        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                return TestUtils.GetRuntimeComponentsCombinations();
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_P2PDifferentFrameworks(string flavor, string os, string architecture)
        {
            string stdOut;
            string stdError;
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                var p1 = Path.Combine(testEnv.RootDir, "P1");
                var p2 = Path.Combine(testEnv.RootDir, "P2");

                Directory.CreateDirectory(p1);
                Directory.CreateDirectory(p2);

                File.WriteAllText($"{p1}/project.json",
                @"{
                    ""dependencies"": {
                        ""System.Runtime"":""4.0.20-*""
                    },
                    ""frameworks"": {
                        ""dotnet"": {}
                    }
                  }");

                File.WriteAllText($"{p1}/BaseClass.cs", @"
public class BaseClass {
    public virtual void Test() { }
}");

                File.WriteAllText($"{p2}/project.json",
                @"{
                    ""dependencies"": {
                        ""P1"":""""
                    },
                    ""frameworks"": {
                        ""dnxcore50"": {}
                    }
                  }");
                File.WriteAllText($"{p2}/TestClass.cs", @"
public class TestClass : BaseClass {
    public override void Test() { }
}");

                var environment = new Dictionary<string, string> { { "DNX_TRACE", "0" } };
                DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", "", out stdOut, out stdError, environment: null, workingDir: testEnv.RootDir);
                var exitCode = DnuTestUtils.ExecDnu(runtimeHomeDir, "pack", "", out stdOut, out stdError, environment, p2);
                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_NoArgs(string flavor, string os, string architecture)
        {
            string expectedDNX =
            @"Building {0} for DNX,Version=v4.5.1
  Using Project dependency {0} 1.0.0
    Source: {1}/project.json".Replace('/', Path.DirectorySeparatorChar);
            string expectedDNXCore =
            @"Building {0} for DNXCore,Version=v5.0
  Using Project dependency {0} 1.0.0
    Source: {1}/project.json".Replace('/', Path.DirectorySeparatorChar);
            string expectedNupkg =
            @"{0} -> {1}/bin/Debug/{0}.1.0.0.nupkg
{0} -> {1}/bin/Debug/{0}.1.0.0.symbols.nupkg".Replace('/', Path.DirectorySeparatorChar);
            string stdOut;
            string stdError;
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            int exitCode;

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                File.WriteAllText($"{testEnv.RootDir}/project.json",
                @"{
                    ""frameworks"": {
                        ""dnx451"": {
                        },
                        ""dnxcore50"": {
                            ""dependencies"": {
                                ""System.Runtime"":""4.0.20-*""
                            }
                        }
                    }
                  }");
                var environment = new Dictionary<string, string> { { "DNX_TRACE", "0" } };
                DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", "", out stdOut, out stdError, environment: null, workingDir: testEnv.RootDir);
                exitCode = DnuTestUtils.ExecDnu(runtimeHomeDir, "pack", "", out stdOut, out stdError, environment, testEnv.RootDir);

                Assert.Empty(stdError);
                Assert.Contains(string.Format(expectedDNX, Path.GetFileName(testEnv.RootDir), testEnv.RootDir), stdOut);
                Assert.Contains(string.Format(expectedDNXCore, Path.GetFileName(testEnv.RootDir), testEnv.RootDir), stdOut);
                Assert.Contains(string.Format(expectedNupkg, Path.GetFileName(testEnv.RootDir), testEnv.RootDir), stdOut);
                Assert.Equal(0, exitCode);
                Assert.True(Directory.Exists($"{testEnv.RootDir}/bin"));
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_FrameworkSpecified(string flavor, string os, string architecture)
        {
            string expectedDNX =
            @"Building {0} for DNX,Version=v4.5.1
  Using Project dependency {0} 1.0.0
    Source: {1}/project.json".Replace('/', Path.DirectorySeparatorChar);
            string expectedNupkg =
            @"{0} -> {1}/bin/Debug/{0}.1.0.0.nupkg
{0} -> {1}/bin/Debug/{0}.1.0.0.symbols.nupkg".Replace('/', Path.DirectorySeparatorChar);
            string stdOut;
            string stdError;
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            int exitCode;

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                File.WriteAllText($"{testEnv.RootDir}/project.json",
                @"{
                    ""frameworks"": {
                        ""dnx451"": {
                        },
                        ""dnxcore50"": {
                            ""dependencies"": {
                                ""System.Runtime"":""4.0.20-*""
                            }
                        }
                    }
                  }");
                var environment = new Dictionary<string, string> { { "DNX_TRACE", "0" } };
                DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", "", out stdOut, out stdError, environment: null, workingDir: testEnv.RootDir);
                exitCode = DnuTestUtils.ExecDnu(runtimeHomeDir, "pack", "--framework dnx451", out stdOut, out stdError, environment, testEnv.RootDir);

                Assert.Empty(stdError);
                Assert.Contains(string.Format(expectedDNX, Path.GetFileName(testEnv.RootDir), testEnv.RootDir), stdOut);
                Assert.DoesNotContain("DNXCore", stdOut);
                Assert.Contains(string.Format(expectedNupkg, Path.GetFileName(testEnv.RootDir), testEnv.RootDir), stdOut);
                Assert.Equal(0, exitCode);
                Assert.True(Directory.Exists($"{testEnv.RootDir}/bin"));
            }
        }

        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_PortablePdbsGeneratedWhenEnvVariableSet(string flavor, string os, string architecture)
        {
            string stdOut;
            string stdError;
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            int exitCode;

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                var appName = PathUtility.GetDirectoryName(testEnv.RootDir);
                File.WriteAllText($"{testEnv.RootDir}/project.json",
                @"{
                    ""frameworks"": {
                        ""dnx451"": {
                        }
                    }
                  }");

                var environment = new Dictionary<string, string> { { "DNX_TRACE", "0" }, { "DNX_BUILD_PORTABLE_PDB", "1" } };
                DnuTestUtils.ExecDnu(runtimeHomeDir,
                                     "restore",
                                     "",
                                     out stdOut,
                                     out stdError,
                                     environment: null,
                                     workingDir: testEnv.RootDir);

                exitCode = DnuTestUtils.ExecDnu(runtimeHomeDir,
                                                "pack",
                                                "",
                                                out stdOut,
                                                out stdError,
                                                environment,
                                                testEnv.RootDir);

                var outputDir = Path.Combine(testEnv.RootDir, "bin", "Debug", "dnx451");
                Assert.Equal(0, exitCode);
                Assert.True(Directory.Exists(outputDir));
                Assert.True(File.Exists(Path.Combine(outputDir, appName + ".dll")));
                Assert.True(File.Exists(Path.Combine(outputDir, appName + ".pdb")));
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_OutPathSpecified(string flavor, string os, string architecture)
        {
            string expectedNupkg =
            @"{0} -> {1}/CustomOutput/Debug/{0}.1.0.0.nupkg".Replace('/', Path.DirectorySeparatorChar);
            string expectedSymbol =
            @"{0} -> {1}/CustomOutput/Debug/{0}.1.0.0.symbols.nupkg".Replace('/', Path.DirectorySeparatorChar);
            string stdOut;
            string stdError;
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            int exitCode;

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                File.WriteAllText($"{testEnv.RootDir}/project.json",
                @"{
                    ""frameworks"": {
                        ""dnx451"": {
                        },
                        ""dnxcore50"": {
                            ""dependencies"": {
                                ""System.Runtime"":""4.0.20-*""
                            }
                        }
                    }
                  }");
                DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", "", out stdOut, out stdError, environment: null, workingDir: testEnv.RootDir);
                exitCode = DnuTestUtils.ExecDnu(runtimeHomeDir, "pack", "--out CustomOutput", out stdOut, out stdError, environment: null, workingDir: testEnv.RootDir);

                Assert.Empty(stdError);
                Assert.Contains(string.Format(expectedNupkg, Path.GetFileName(testEnv.RootDir), testEnv.RootDir), stdOut);
                Assert.Contains(string.Format(expectedSymbol, Path.GetFileName(testEnv.RootDir), testEnv.RootDir), stdOut);
                Assert.Equal(0, exitCode);
                Assert.True(Directory.Exists($"{testEnv.RootDir}/CustomOutput"));
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_NormalizesVersionNumberWithRevisionNumberOfZero(string flavor, string os, string architecture)
        {
            int exitCode;
            var projectName = "TestProject";
            var projectStructure = @"{
  '.': ['project.json']
}";
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, projectName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""version"": ""1.0.0.0"",
  ""frameworks"": {
    ""dnx451"": {}
  }
}")
                    .WriteTo(testEnv.ProjectPath);

                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "restore",
                    arguments: string.Empty,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "pack",
                    arguments: string.Empty,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                Assert.True(File.Exists(Path.Combine(testEnv.ProjectPath, "bin", "Debug", $"{projectName}.1.0.0.nupkg")));
                Assert.True(File.Exists(Path.Combine(testEnv.ProjectPath, "bin", "Debug", $"{projectName}.1.0.0.symbols.nupkg")));
            }
        }
        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_NormalizesVersionNumberWithNoBuildNumber(string flavor, string os, string architecture)
        {
            int exitCode;
            var projectName = "TestProject";
            var projectStructure = @"{
  '.': ['project.json']
}";
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, projectName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""version"": ""1.0-beta"",
  ""frameworks"": {
    ""dnx451"": {}
  }
}")
                    .WriteTo(testEnv.ProjectPath);

                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "restore",
                    arguments: string.Empty,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "pack",
                    arguments: string.Empty,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                Assert.True(File.Exists(Path.Combine(testEnv.ProjectPath, "bin", "Debug", $"{projectName}.1.0.0-beta.nupkg")));
                Assert.True(File.Exists(Path.Combine(testEnv.ProjectPath, "bin", "Debug", $"{projectName}.1.0.0-beta.symbols.nupkg")));
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_DoesNotExecutePostBuildScriptWhenBuildFails(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            var projectJson = @"{
  ""frameworks"": {
      ""dnx451"": { },
      ""dnxcore50"": { }
  },
  ""scripts"": {
    ""postbuild"": ""echo POST_BUILD_SCRIPT_OUTPUT"",
    ""postpack"": ""echo POST_PACK_SCRIPT_OUTPUT""
  },
}";
            var sourceFileContents = @"Invalid source code that makes build fail";

            using (var tempDir = new DisposableDir())
            {
                var projectJsonPath = Path.Combine(tempDir, Runtime.Project.ProjectFileName);
                var sourceFilePath = Path.Combine(tempDir, "Program.cs");
                File.WriteAllText(projectJsonPath, projectJson);
                File.WriteAllText(sourceFilePath, sourceFileContents);

                string stdOut, stdErr;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    "pack",
                    projectJsonPath,
                    out stdOut,
                    out stdErr);

                Assert.Equal(1, exitCode);
                Assert.NotEmpty(stdErr);
                Assert.DoesNotContain("POST_BUILD_SCRIPT_OUTPUT", stdOut);
                Assert.DoesNotContain("POST_PACK_SCRIPT_OUTPUT", stdOut);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_ExecutesScriptsForEachConfigurationAndTargetFramework(string flavor, string os, string architecture)
        {
            var projectStructure = @"{
  '.': ['project.json']
}";
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, "TestProject"))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""version"": ""1.0-beta"",
  ""frameworks"": {
    ""dnx451"": {},
    ""dnxcore50"": {
        ""dependencies"": {
            ""System.Runtime"":""4.0.20-*""
        }
      }
  },
  ""scripts"": {
    ""prebuild"": ""echo PREBUILD_SCRIPT_OUTPUT %build:Configuration% %build:TargetFramework%"",
    ""prepack"": ""echo PREPACK_SCRIPT_OUTPUT %build:Configuration% %build:TargetFramework%"",
    ""postbuild"": ""echo POSTBUILD_SCRIPT_OUTPUT %build:Configuration% %build:TargetFramework%"",
    ""postpack"": ""echo POSTPACK_SCRIPT_OUTPUT %build:Configuration% %build:TargetFramework%""
  }
}")
                    .WriteTo(testEnv.ProjectPath);

                string stdOut, stdErr;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "restore",
                    arguments: string.Empty,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    "pack",
                    testEnv.ProjectPath + " --configuration Debug --configuration Release",
                    out stdOut,
                    out stdErr);

                Assert.Equal(0, exitCode);
                Assert.Empty(stdErr);

                var idx = 0;
                foreach (var configuration in new[] { "Debug", "Release"})
                {
                    // note that %TargetFramework% is not defined outside build
                    idx = stdOut.IndexOf($"PREPACK_SCRIPT_OUTPUT {configuration} %build:TargetFramework%", 0);
                    Assert.True(idx >= 0);
                    foreach (var framework in new[] { "dnx451", "dnxcore50" })
                    {
                        idx = stdOut.IndexOf($"PREBUILD_SCRIPT_OUTPUT {configuration} {framework}", idx);
                        Assert.True(idx >= 0);
                        idx = stdOut.IndexOf($"POSTBUILD_SCRIPT_OUTPUT {configuration} {framework}", idx);
                        Assert.True(idx >= 0);
                    }
                    idx = stdOut.IndexOf($"POSTPACK_SCRIPT_OUTPUT {configuration} %build:TargetFramework%", 0);
                    Assert.True(idx >= 0);
                }

                Assert.Equal(-1, stdOut.IndexOf("PREPACK_SCRIPT_OUTPUT", idx + 1));
                Assert.Equal(-1, stdOut.IndexOf("PREBUILD_SCRIPT_OUTPUT", idx + 1));
                Assert.Equal(-1, stdOut.IndexOf("POSTBUILD_SCRIPT_OUTPUT", idx + 1));
                Assert.Equal(-1, stdOut.IndexOf("POSTPACK_SCRIPT_OUTPUT", idx + 1));
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_ShowUnresolvedDependencyWhenBuildFails(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            var projectJson = @"{
  ""frameworks"": {
    ""dnx451"": {
      ""dependencies"": {
      ""NonexistentPackage"": ""1.0.0""
      }
    }
  }
}";

            using (var tempDir = new DisposableDir())
            {
                var projectPath = Path.Combine(tempDir, "Project");
                var emptyLocalFeed = Path.Combine(tempDir, "EmptyLocalFeed");
                Directory.CreateDirectory(projectPath);
                Directory.CreateDirectory(emptyLocalFeed);
                var projectJsonPath = Path.Combine(projectPath, Runtime.Project.ProjectFileName);
                File.WriteAllText(projectJsonPath, projectJson);

                string stdOut, stdErr;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    "restore",
                    $"{projectJsonPath} -s {emptyLocalFeed}",
                    out stdOut,
                    out stdErr);
                Assert.Equal(1, exitCode);

                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    "pack",
                    projectJsonPath,
                    out stdOut,
                    out stdErr);

                Assert.Equal(1, exitCode);
                Assert.NotEmpty(stdErr);
                var unresolvedDependencyErrorCount = stdErr
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.Contains("The dependency NonexistentPackage >= 1.0.0 could not be resolved"))
                    .Count();
                Assert.Equal(1, unresolvedDependencyErrorCount);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_ResourcesNoArgs_WarningAsErrorsCompilationOption(string flavor, string os, string architecture)
        {
            string stdOut;
            string stdError;
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            int exitCode;

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                using (var tempDir = new DisposableDir())
                {
                    var appPath = Path.Combine(tempDir, "ResourcesTestProjects", "ReadFromResources");
                    TestUtils.CopyFolder(Path.Combine(TestUtils.GetMiscProjectsFolder(), "ResourcesTestProjects", "ReadFromResources"), appPath);
                    var workingDir = Path.Combine(appPath, "src", "ResourcesLibrary");

                    var environment = new Dictionary<string, string> { { "DNX_TRACE", "0" } };
                    DnuTestUtils.ExecDnu(
                        runtimeHomeDir,
                        "restore", "",
                        out stdOut,
                        out stdError,
                        environment: null,
                        workingDir: workingDir);
                    exitCode = DnuTestUtils.ExecDnu(
                        runtimeHomeDir,
                        "pack",
                        "",
                        out stdOut,
                        out stdError,
                        environment,
                        workingDir);

                    Assert.Empty(stdError);
                    Assert.Equal(0, exitCode);
                    Assert.True(Directory.Exists(Path.Combine(workingDir, "bin")));
                    Assert.True(File.Exists(Path.Combine(workingDir, "bin", "Debug", "dnx451", "fr-FR", "ResourcesLibrary.resources.dll")));
                    Assert.True(File.Exists(Path.Combine(workingDir, "bin", "Debug", "dnxcore50", "fr-FR", "ResourcesLibrary.resources.dll")));
                }
            }
        }
    }
}
