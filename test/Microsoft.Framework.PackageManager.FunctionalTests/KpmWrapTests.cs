// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.FunctionalTestUtils;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Framework.PackageManager
{
    public class KpmWrapTests
    {
        public static IEnumerable<object[]> KrePaths
        {
            get
            {
                return TestUtils.GetUnpackedKrePaths().Select(path => new[] { path });
            }
        }

        public static readonly string _msbuildPath = TestUtils.ResolveMSBuildPath();

        [Theory]
        [MemberData("KrePaths")]
        public void KpmWrapUpdatesExistingProjectJson(DisposableDirPath krePathDir)
        {
            if (PlatformHelper.IsMono)
            {
                return;
            }

            var expectedProjectJson = @"{
  ""version"": ""1.0.0-*"",
  ""dependencies"": {},
  ""frameworks"": {
    ""net45+win+wpa81+wp80"": {
      ""wrappedProject"": ""../../LibraryBeta.PCL/LibraryBeta.PCL.csproj"",
      ""bin"": {
        ""assembly"": ""../../LibraryBeta.PCL/obj/{configuration}/LibraryBeta.dll"",
        ""pdb"": ""../../LibraryBeta.PCL/obj/{configuration}/LibraryBeta.pdb""
      }
    },
    ""net45"": {
      ""wrappedProject"": ""../../LibraryBeta.PCL.Desktop/LibraryBeta.PCL.Desktop.csproj"",
      ""bin"": {
        ""assembly"": ""../../LibraryBeta.PCL.Desktop/obj/{configuration}/LibraryBeta.dll"",
        ""pdb"": ""../../LibraryBeta.PCL.Desktop/obj/{configuration}/LibraryBeta.pdb""
      }
    },
    ""wpa81"": {
      ""wrappedProject"": ""../../LibraryBeta.PCL.Phone/LibraryBeta.PCL.Phone.csproj"",
      ""bin"": {
        ""assembly"": ""../../LibraryBeta.PCL.Phone/obj/{configuration}/LibraryBeta.dll"",
        ""pdb"": ""../../LibraryBeta.PCL.Phone/obj/{configuration}/LibraryBeta.pdb""
      }
    }
  }
}";
            var expectedGlobalJson = @"{
    ""sources"": [ ""src"", ""test"" ]
}";
            using (krePathDir)
            using (var testSolutionDir = TestUtils.GetTempTestSolution("ConsoleApp1"))
            {
                var libBetaPclCsprojPath = Path.Combine(testSolutionDir, "LibraryBeta.PCL", "LibraryBeta.PCL.csproj");
                var libBetaPclDesktopCsprojPath = Path.Combine(
                    testSolutionDir, "LibraryBeta.PCL.Desktop", "LibraryBeta.PCL.Desktop.csproj");
                var libBetaPclPhoneCsprojPath = Path.Combine(
                    testSolutionDir, "LibraryBeta.PCL.Phone", "LibraryBeta.PCL.Phone.csproj");
                var libBetaJsonPath = Path.Combine(testSolutionDir, "src", "LibraryBeta", "project.json");
                var globalJsonPath = Path.Combine(testSolutionDir, "global.json");

                var betaPclExitCode = KpmTestUtils.ExecKpm(
                    krePath: krePathDir,
                    subcommand: "wrap",
                    arguments: string.Format("\"{0}\" --msbuild \"{1}\"", libBetaPclCsprojPath, _msbuildPath));

                var betaDesktopExitCode = KpmTestUtils.ExecKpm(
                    krePath: krePathDir,
                    subcommand: "wrap",
                    arguments: string.Format("\"{0}\" --msbuild \"{1}\"", libBetaPclDesktopCsprojPath, _msbuildPath));

                var betaPhoneExitCode = KpmTestUtils.ExecKpm(
                    krePath: krePathDir,
                    subcommand: "wrap",
                    arguments: string.Format("\"{0}\" --msbuild \"{1}\"", libBetaPclPhoneCsprojPath, _msbuildPath));

                Assert.Equal(0, betaPclExitCode);
                Assert.Equal(0, betaDesktopExitCode);
                Assert.Equal(0, betaPhoneExitCode);
                Assert.Equal(expectedGlobalJson, File.ReadAllText(globalJsonPath));
                Assert.False(Directory.Exists(Path.Combine(testSolutionDir, "wrap")));
                Assert.Equal(expectedProjectJson, File.ReadAllText(libBetaJsonPath));
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmWrapMaintainsAllKindsOfReferences(DisposableDirPath krePathDir)
        {
            if (PlatformHelper.IsMono)
            {
                return;
            }

            var expectedLibGammaProjectJson = @"{
  ""version"": ""1.0.0-*"",
  ""frameworks"": {
    ""net45"": {
      ""wrappedProject"": ""../../LibraryGamma/LibraryGamma.csproj"",
      ""bin"": {
        ""assembly"": ""../../LibraryGamma/obj/{configuration}/LibraryGamma.dll"",
        ""pdb"": ""../../LibraryGamma/obj/{configuration}/LibraryGamma.pdb""
      },
      ""dependencies"": {
        ""EntityFramework"": ""6.1.2-beta1"",
        ""LibraryEpsilon"": ""1.0.0-*"",
        ""LibraryDelta"": ""1.0.0-*""
      }
    }
  }
}";
            var expectedLibEpsilonProjectJson = @"{
  ""version"": ""1.0.0-*"",
  ""frameworks"": {
    ""net45"": {
      ""wrappedProject"": ""../../LibraryEpsilon/LibraryEpsilon.csproj"",
      ""bin"": {
        ""assembly"": ""../../LibraryEpsilon/obj/{configuration}/LibraryEpsilon.dll"",
        ""pdb"": ""../../LibraryEpsilon/obj/{configuration}/LibraryEpsilon.pdb""
      }
    }
  }
}";
            var expectedLibDeltaProjectJson = @"{
  ""version"": ""1.0.0-*"",
  ""frameworks"": {
    ""net45"": {
      ""bin"": {
        ""assembly"": ""../../ExternalAssemblies/LibraryDelta.dll""
      }
    }
  }
}";
            var expectedGlobalJson = @"{
  ""sources"": [
    ""src"",
    ""test"",
    ""wrap""
  ]
}";
            using (krePathDir)
            using (var testSolutionDir = TestUtils.GetTempTestSolution("ConsoleApp1"))
            {
                var libGammaCsprojPath = Path.Combine(testSolutionDir, "LibraryGamma", "LibraryGamma.csproj");
                var globalJsonPath = Path.Combine(testSolutionDir, "global.json");
                var wrapFolderPath = Path.Combine(testSolutionDir, "wrap");
                var libGammaJsonPath = Path.Combine(wrapFolderPath, "LibraryGamma", "project.json");
                var libEpsilonJsonPath = Path.Combine(wrapFolderPath, "LibraryEpsilon", "project.json");
                var libDeltaJsonPath = Path.Combine(wrapFolderPath, "LibraryDelta", "project.json");

                var exitCode = KpmTestUtils.ExecKpm(
                    krePath: krePathDir,
                    subcommand: "wrap",
                    arguments: string.Format("\"{0}\" --msbuild \"{1}\"", libGammaCsprojPath, _msbuildPath));

                Assert.Equal(0, exitCode);
                Assert.Equal(expectedGlobalJson, File.ReadAllText(globalJsonPath));
                Assert.Equal(3, Directory.EnumerateDirectories(wrapFolderPath).Count());
                Assert.Equal(expectedLibGammaProjectJson, File.ReadAllText(libGammaJsonPath));
                Assert.Equal(expectedLibEpsilonProjectJson, File.ReadAllText(libEpsilonJsonPath));
                Assert.Equal(expectedLibDeltaProjectJson, File.ReadAllText(libDeltaJsonPath));
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void RunAppWithWrappedProjects(DisposableDirPath krePathDir)
        {
            if (PlatformHelper.IsMono)
            {
                return;
            }

            var expectedConsoleApp1OutputLines = new[] {
                "Referencing LibraryBeta.dll",
                "Referencing LibraryEpsilon.dll",
                "Referencing LibraryGamma.dll",
                "Referencing EntityFramework.dll",
                "Referencing EntityFramework.SqlServer.dll",
                "Referencing LibraryDelta.dll"
            };

            var expectedConsoleApp1Output = string.Join(Environment.NewLine, expectedConsoleApp1OutputLines) + Environment.NewLine;

            using (krePathDir)
            using (var testSolutionDir = TestUtils.GetTempTestSolution("ConsoleApp1"))
            using (var x86ClrKrePath = TestUtils.GetUnpackedKrePath(flavor: "CLR", architecture: "x86"))
            {
                var libBetaPclDesktopCsprojPath = Path.Combine(
                    testSolutionDir, "LibraryBeta.PCL.Desktop", "LibraryBeta.PCL.Desktop.csproj");
                var libGammaCsprojPath = Path.Combine(testSolutionDir, "LibraryGamma", "LibraryGamma.csproj");
                var consoleApp1Folder = Path.Combine(testSolutionDir, "src", "ConsoleApp1");
                var consoleApp1ProjectJson = Path.Combine(consoleApp1Folder, "project.json");

                // Add LibraryGamma as a dependency
                var root = JObject.Parse(File.ReadAllText(consoleApp1ProjectJson));
                root["dependencies"]["LibraryGamma"] = "1.0.0-*";
                File.WriteAllText(consoleApp1ProjectJson, root.ToString());

                string stdOut, stdErr;
                var betaBuildExitCode = TestUtils.Exec(
                    program: _msbuildPath,
                    commandLine: string.Format("\"{0}\"", libBetaPclDesktopCsprojPath),
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                // For diaganostics
                if (betaBuildExitCode != 0)
                {
                    Console.WriteLine("Failed to build LibraryBeta with '{0}'", _msbuildPath);
                    Console.WriteLine(stdOut);
                    Console.WriteLine(stdErr);
                }

                var gammaBuildExitCode = TestUtils.Exec(
                    program: _msbuildPath,
                    commandLine: string.Format("\"{0}\"", libGammaCsprojPath),
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                // For diaganostics
                if (gammaBuildExitCode != 0)
                {
                    Console.WriteLine("Failed to build LibraryGamma with '{0}'", _msbuildPath);
                    Console.WriteLine(stdOut);
                    Console.WriteLine(stdErr);
                }

                var betaDesktopWrapExitCode = KpmTestUtils.ExecKpm(
                    krePath: krePathDir,
                    subcommand: "wrap",
                    arguments: string.Format("\"{0}\" --msbuild \"{1}\"", libBetaPclDesktopCsprojPath, _msbuildPath));

                var gammaWrapExitCode = KpmTestUtils.ExecKpm(
                    krePath: krePathDir,
                    subcommand: "wrap",
                    arguments: string.Format("\"{0}\" --msbuild \"{1}\"", libGammaCsprojPath, _msbuildPath));

                var consoleApp1ExitCode = TestUtils.Exec(
                    program: Path.Combine(x86ClrKrePath, "bin", "k.cmd"),
                    commandLine: "run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string>
                    {
                        { "K_APPBASE", consoleApp1Folder },
                        { "KRE_TRACE", null }  // Turn off KRE_TRACE because we need to verify app output
                    });

                // For diaganostics
                if (consoleApp1ExitCode != 0)
                {
                    Console.WriteLine("Failed to run ConsoleApp1");
                    Console.WriteLine(stdOut);
                    Console.WriteLine(stdErr);
                }

                Assert.Equal(0, betaDesktopWrapExitCode);
                Assert.Equal(0, gammaWrapExitCode);
                Assert.Equal(0, consoleApp1ExitCode);
                Assert.Equal(expectedConsoleApp1Output, stdOut);
            }
        }
    }
}
