// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.FunctionalTestUtils;
using Microsoft.Framework.PackageManager.Utils;
using Xunit;

namespace Microsoft.Framework.PackageManager.Tests
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
            var projectDir = Path.Combine(rootDir, "misc", "ServicingTestProjects", projectName);
            const string configuration = "Debug";

            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor: "clr", os: "win", architecture: "x86"))
            using (var tempDir = new DisposableDir())
            {
                var buildOutpuDir = Path.Combine(tempDir, "output");

                KpmTestUtils.ExecKpm(
                    runtimeHomeDir,
                    "pack",
                    $"{projectDir} --out {buildOutpuDir} --configuration {configuration}");

                var assemblyPath = Path.Combine(buildOutpuDir, configuration, "dnx451", $"{projectName}.dll");
                Assert.Equal(expectedServiceability, LockFileUtils.IsAssemblyServiceable(assemblyPath));
            }
        }
    }
}