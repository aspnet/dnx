// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Tooling.Utils;
using Microsoft.Dnx.Runtime;
using Xunit;

namespace Microsoft.Dnx.Tooling.Tests
{
    public class LockFileUtilsFacts
    {
        [Theory]
        [InlineData("ServiceableLib1", true)]
        [InlineData("UnserviceableLib1", false)]
        [InlineData("UnserviceableLib2", false)]
        public void BuildPackageAndCheckServiceability(string projectName, bool expectedServiceability)
        {
            var rootDir = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var projectSrcDir = Path.Combine(rootDir, "misc", "ServicingTestProjects", projectName);
            const string configuration = "Debug";

            var components = TestUtils.GetRuntimeComponentsCombinations().First();
            var flavor = (string)components[0];
            var os = (string)components[1];
            var architecture = (string)components[2];

            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            using (var tempDir = new DisposableDir())
            {
                var projectDir = Path.Combine(tempDir, projectName);
                var buildOutpuDir = Path.Combine(tempDir, "output");
                TestUtils.CopyFolder(projectSrcDir, projectDir);

                var exitCode = DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", projectDir);
                Assert.Equal(0, exitCode);

                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    "pack",
                    $"{projectDir} --out {buildOutpuDir} --configuration {configuration}");

                Assert.Equal(0, exitCode);

                var assemblyPath = Path.Combine(buildOutpuDir, configuration, "dnx451", $"{projectName}.dll");
                Assert.Equal(expectedServiceability, LockFileUtils.IsAssemblyServiceable(assemblyPath));
            }
        }
    }
}