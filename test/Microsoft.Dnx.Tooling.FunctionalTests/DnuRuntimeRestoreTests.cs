// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    public class DnuRuntimeRestoreTests
    {
        private readonly IApplicationEnvironment _appEnv;
        private static readonly FrameworkName Dnx451 = VersionUtility.ParseFrameworkName("dnx451");

        public DnuRuntimeRestoreTests()
        {
            _appEnv = (IApplicationEnvironment)CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof(IApplicationEnvironment));
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
        public void DnuRestore_GeneratesDefaultRuntimeTargets(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            LockFile lockFile;
            using (var testDir = new DisposableDir())
            {
                DirTree.CreateFromDirectory(Path.Combine(_appEnv.ApplicationBasePath, "..", "..", "misc", "RuntimeRestore", "TestProject"))
                    .WriteTo(testDir);

                // Clean up the lock file if it ended up there during the copy
                var lockFilePath = Path.Combine(testDir, "project.lock.json");
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }

                // Restore the project!
                var source = Path.Combine(_appEnv.ApplicationBasePath, "..", "..", "misc", "RuntimeRestore", "RuntimeRestoreTestPackage", "feed");
                DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", $"--source {source}", workingDir: testDir);

                // Check the lock file
                lockFile = (new LockFileFormat()).Read(Path.Combine(testDir, "project.lock.json"));
            }

            // We can use the runtime environment to determine the expected RIDs because by default it only uses the current OSes RIDs
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                AssertLockFileTarget(lockFile, "win7-x86", "win7-x86");
                AssertLockFileTarget(lockFile, "win7-x64", "win7-x64");
            }
            else
            {
                var osName = RuntimeEnvironmentHelper.RuntimeEnvironment.OperatingSystem.ToLowerInvariant();
                AssertLockFileTarget(lockFile, osName + "-x86", assemblyRid: null); // There is no linux/darwin-x86 in the test package
                AssertLockFileTarget(lockFile, osName + "-x64", osName + "-x64");
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuRestore_UsesProjectAndCommandLineProvidedRuntimes(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            LockFile lockFile;
            using (var testDir = new DisposableDir())
            {
                DirTree.CreateFromDirectory(Path.Combine(_appEnv.ApplicationBasePath, "..", "..", "misc", "RuntimeRestore", "TestProject"))
                    .WriteTo(testDir);

                // Clean up the lock file if it ended up there during the copy
                var lockFilePath = Path.Combine(testDir, "project.lock.json");
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }

                // Modify the project
                AddRuntimeToProject(testDir, "win10-x86");

                // Restore the project!
                var source = Path.Combine(_appEnv.ApplicationBasePath, "..", "..", "misc", "RuntimeRestore", "RuntimeRestoreTestPackage", "feed");
                DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", $"--source {source} --runtime linux-x64 --runtime darwin-x64", workingDir: testDir);

                // Check the lock file
                lockFile = (new LockFileFormat()).Read(Path.Combine(testDir, "project.lock.json"));
            }

            AssertLockFileTarget(lockFile, "win10-x86", "win8-x86");
            AssertLockFileTarget(lockFile, "darwin-x64", "darwin-x64");
            AssertLockFileTarget(lockFile, "linux-x64", "linux-x64");
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuRestore_DoesFallback(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            LockFile lockFile;
            using (var testDir = new DisposableDir())
            {
                DirTree.CreateFromDirectory(Path.Combine(_appEnv.ApplicationBasePath, "..", "..", "misc", "RuntimeRestore", "TestProject"))
                    .WriteTo(testDir);

                // Clean up the lock file if it ended up there during the copy
                var lockFilePath = Path.Combine(testDir, "project.lock.json");
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }

                // Restore the project!
                var source = Path.Combine(_appEnv.ApplicationBasePath, "..", "..", "misc", "RuntimeRestore", "RuntimeRestoreTestPackage", "feed");
                DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", $"--source {source} --runtime win10-x64 --runtime win10-x86 --runtime linux-x86", workingDir: testDir);

                // Check the lock file
                lockFile = (new LockFileFormat()).Read(Path.Combine(testDir, "project.lock.json"));
            }

            AssertLockFileTarget(lockFile, "win10-x64", "win7-x64");
            AssertLockFileTarget(lockFile, "win10-x86", "win8-x86");
            AssertLockFileTarget(lockFile, "linux-x86", assemblyRid: null);
        }

        private void AddRuntimeToProject(string projectRoot, string rid)
        {
            var projectFile = Path.Combine(projectRoot, "project.json");
            var json = JObject.Parse(File.ReadAllText(projectFile));
            json["runtimes"] = new JObject(new JProperty(rid, new JObject()));
            File.WriteAllText(projectFile, json.ToString());
        }

        private void AssertLockFileTarget(LockFile lockFile, string searchRid, string assemblyRid)
        {
            var target = lockFile.Targets.SingleOrDefault(t => t.TargetFramework == Dnx451 && string.Equals(t.RuntimeIdentifier, searchRid, StringComparison.Ordinal));
            Assert.NotNull(target);
            var library = target.Libraries.SingleOrDefault(l => l.Name.Equals("RuntimeRestoreTest"));
            Assert.NotNull(library);

            if (string.IsNullOrEmpty(assemblyRid))
            {
                AssertLockFileItemPath("lib/dnx451/RuntimeRestoreTest.dll", library.CompileTimeAssemblies.Single());
                AssertLockFileItemPath("lib/dnx451/RuntimeRestoreTest.dll", library.RuntimeAssemblies.Single());
            }
            else
            {
                AssertLockFileItemPath($"runtimes/{assemblyRid}/lib/dnx451/RuntimeRestoreTest.dll", library.CompileTimeAssemblies.Single());
                AssertLockFileItemPath($"runtimes/{assemblyRid}/lib/dnx451/RuntimeRestoreTest.dll", library.RuntimeAssemblies.Single());
            }
        }

        private void AssertLockFileItemPath(string path, LockFileItem item)
        {
            Assert.NotNull(item);
            Assert.Equal(path, PathUtility.GetPathWithForwardSlashes(item.Path));
        }
    }
}
