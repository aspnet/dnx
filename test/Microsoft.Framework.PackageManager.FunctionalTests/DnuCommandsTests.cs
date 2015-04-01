// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.CommonTestUtils;
using Microsoft.Framework.Runtime.DependencyManagement;
using Xunit;

namespace Microsoft.Framework.PackageManager
{
    public class DnuCommandsTests
    {
        public static IEnumerable<object[]> RuntimeComponents
        {
            get { return TestUtils.GetRuntimeComponentsCombinations(); }
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
                Directory.CreateDirectory($"{directory}/packages/pack2/0.0.0/");
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
            Directory.CreateDirectory($"{directory}/packages/{name}/{version}/app");
            File.WriteAllText($"{directory}/packages/{name}/{version}/app/{name}.cmd", "");
            File.WriteAllText($"{directory}/{name}.cmd", $"~dp0/packages/{name}/{version}/app/{name}.cmd".Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText($"{directory}/packages/{name}/{version}/{name}.{version}.nupkg.sha512", "TestSha");
        }

        private void WriteLockFile(string dir, string libName, string version)
        {
            var lockFile = new LockFile
            {
                Islocked = false,
                Libraries = new List<LockFileLibrary>
                            {
                                new LockFileLibrary
                                {
                                    Name = libName,
                                    Version = new NuGet.SemanticVersion(version),
                                    Sha512 = "TestSha"
                                }
                            }
            };
            var lockFormat = new LockFileFormat();
            lockFormat.Write($"{dir}/project.lock.json", lockFile);
        }
    }
}