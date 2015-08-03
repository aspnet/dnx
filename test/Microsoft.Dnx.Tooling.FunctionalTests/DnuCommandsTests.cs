// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Runtime;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    [Collection(nameof(PackageManagerFunctionalTestCollection))]
    public class DnuCommandsTests
    {
        private readonly PackageManagerFunctionalTestFixture _fixture;

        public DnuCommandsTests(PackageManagerFunctionalTestFixture fixture)
        {
            _fixture = fixture;
        }

        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                return TestUtils.GetRuntimeComponentsCombinations();
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuCommands_Install_InstallsWorkingCommand(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                var environment = new Dictionary<string, string>();
                environment.Add("USERPROFILE", testEnv.RootDir);

                string stdOut, stdErr;
                var exitCode = DnuTestUtils.ExecDnu(runtimeHomeDir, "commands",
                    $"install {_fixture.PackageSource}/Debug/CommandsProject.1.0.0.nupkg --source https://nuget.org/api/v2/",
                    out stdOut, out stdErr, environment, workingDir: testEnv.RootDir);

                var commandFilePath = "hello.cmd";
                bool isWindows = TestUtils.CurrentRuntimeEnvironment.OperatingSystem == "Windows";
                if (!isWindows)
                {
                    commandFilePath = "hello";
                }
                commandFilePath = Path.Combine(testEnv.RootDir, ".dnx/bin", commandFilePath);

                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrEmpty(stdErr));
                Assert.True(File.Exists(commandFilePath));

                environment = new Dictionary<string, string>();
                environment.Add("DNX_PACKAGES", null);

                if (!isWindows)
                {
                    exitCode = TestUtils.Exec("bash", commandFilePath, out stdOut, out stdErr, environment);
                }
                else
                {
                    exitCode = TestUtils.Exec("cmd", $"/C {commandFilePath}", out stdOut, out stdErr, environment);
                }
                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrEmpty(stdErr));
                Assert.Contains("Write text", stdOut);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuCommands_Uninstall_PreservesPackagesUsedByOtherInstalledApps(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                var directory = Path.Combine(testEnv.RootDir, Runtime.Constants.DefaultLocalRuntimeHomeDir, "bin");
                InstallFakeApp(directory, "pack1", "0.0.0");
                InstallFakeApp(directory, "pack2", "0.0.0");
                WriteLockFile($"{directory}/packages/pack2/0.0.0/app", "pack2", "0.0.0");

                var environment = new Dictionary<string, string> { { "USERPROFILE", testEnv.RootDir } };
                DnuTestUtils.ExecDnu(runtimeHomeDir, "commands", "uninstall pack1", environment);

                // Pack2 is in use by the pack2 app so should not be removed
                Assert.True(Directory.Exists($"{directory}/packages/pack2"));
                // Pack1 only used by pack1 app so should be removed
                Assert.False(Directory.Exists($"{directory}/packages/pack1"));
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuCommands_Uninstall_RemovesUnusedPackageNotUsedByApp(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                var directory = Path.Combine(testEnv.RootDir, Runtime.Constants.DefaultLocalRuntimeHomeDir, "bin");
                InstallFakeApp(directory, "pack1", "0.0.0");
                InstallFakePackage(directory, "pack2", "0.0.0");
                WriteLockFile($"{directory}/packages/pack1/0.0.0/app", "pack1", "0.0.0");

                var environment = new Dictionary<string, string> { { "USERPROFILE", testEnv.RootDir } };
                DnuTestUtils.ExecDnu(runtimeHomeDir, "commands", "uninstall pack1", environment);

                // Pack1 only used by pack1 app so should be removed
                Assert.False(Directory.Exists($"{directory}/packages/pack1"));
                // Pack2 was unused so should be removed
                Assert.False(Directory.Exists($"{directory}/packages/pack2"));
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuCommands_Uninstall_NoPurge_DoesNotRemoveUnusedPackageNotUsedByApp(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                var directory = Path.Combine(testEnv.RootDir, Runtime.Constants.DefaultLocalRuntimeHomeDir, "bin");
                InstallFakeApp(directory, "pack1", "0.0.0");
                Directory.CreateDirectory($"{directory}/packages/pack2/0.0.0/");
                WriteLockFile($"{directory}/packages/pack1/0.0.0/app", "pack1", "0.0.0");

                var environment = new Dictionary<string, string> { { "USERPROFILE", testEnv.RootDir } };
                DnuTestUtils.ExecDnu(runtimeHomeDir, "commands", "uninstall pack1 --no-purge", environment);

                // Pack1 only used by pack1 app but --no-purge should not remove it
                Assert.True(Directory.Exists($"{directory}/packages/pack1"));
                // Pack2 was unused but --no-purge should not remove it
                Assert.True(Directory.Exists($"{directory}/packages/pack2"));
            }
        }

        private void InstallFakeApp(string directory, string name, string version)
        {
            InstallFakePackage(directory, name, version);
            Directory.CreateDirectory($"{directory}/packages/{name}/{version}/app");
            File.WriteAllText($"{directory}/packages/{name}/{version}/app/{name}.cmd", "");
            File.WriteAllText($"{directory}/{name}.cmd", $"~dp0/packages/{name}/{version}/app/{name}.cmd".Replace('/', Path.DirectorySeparatorChar));
        }

        private void InstallFakePackage(string directory, string name, string version)
        {
            Directory.CreateDirectory($"{directory}/packages/{name}/{version}");
            File.WriteAllText($"{directory}/packages/{name}/{version}/{name}{NuGet.Constants.ManifestExtension}", "");
            File.WriteAllText($"{directory}/packages/{name}/{version}/{name}.{version}.nupkg.sha512", "TestSha");
        }

        private void WriteLockFile(string dir, string libName, string version)
        {
            var lockFile = new LockFile
            {
                Islocked = false,
                PackageLibraries = new List<LockFilePackageLibrary>
                {
                    new LockFilePackageLibrary
                    {
                        Name = libName,
                        Version = new SemanticVersion(version),
                        Sha512 = "TestSha"
                    }
                }
            };
            var lockFormat = new LockFileFormat();
            lockFormat.Write($"{dir}/project.lock.json", lockFile);
        }
    }
}
