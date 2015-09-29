// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Dnx.Testing.Framework;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    public class DnuRestoreTests : DnxSdkFunctionalTestBase
    {
        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void Restore_PackageOverProjectTarget(DnxSdk sdk)
        {
            var solution = TestUtils.GetSolution<DnuRestoreTests>(sdk, "DependencyTargets");
            var bOutputPath = Path.Combine(solution.ArtifactsPath, "B");

            // Build a package for B
            sdk.Dnu.Restore(solution.GetProject("A")).EnsureSuccess();
            sdk.Dnu.Restore(solution.GetProject("B")).EnsureSuccess();
            sdk.Dnu.Pack(
                solution.GetProject("B").ProjectDirectory,
                bOutputPath,
                configuration: "package").EnsureSuccess();
            sdk.Dnu.PackagesAdd(
                Path.Combine(bOutputPath, "package", "B.1.0.0.nupkg"),
                solution.LocalPackagesDir).EnsureSuccess();

            // Restore the app, it'll work but it will choose the package over the project
            sdk.Dnu.Restore(solution.GetProject("App")).EnsureSuccess();

            // Run the app
            var result = sdk.Dnx.Execute(solution.GetProject("App"));

            Assert.Contains(@"A: This is Project A
B: This is Package B
", result.StandardOutput);

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void Restore_ProjectOverPackageTarget(DnxSdk sdk)
        {
            var solution = TestUtils.GetSolution<DnuRestoreTests>(sdk, "DependencyTargets");
            var aOutputPath = Path.Combine(solution.ArtifactsPath, "A");

            sdk.Dnu.Restore(solution.GetProject("B")).EnsureSuccess();

            // Build a package for A
            sdk.Dnu.Restore(solution.GetProject("A")).EnsureSuccess();
            sdk.Dnu.Pack(
                solution.GetProject("A").ProjectDirectory,
                aOutputPath,
                configuration: "package").EnsureSuccess();
            sdk.Dnu.PackagesAdd(
                Path.Combine(aOutputPath, "package", "A.1.0.0.nupkg"),
                solution.LocalPackagesDir).EnsureSuccess();

            // Delete the project A
            CommonTestUtils.TestUtils.DeleteFolder(solution.GetProject("A").ProjectDirectory);

            // Restore the app, it should fail because the project is gone!
            var result = sdk.Dnu.Restore(solution.GetProject("App"));

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Unable to locate Project A", result.StandardError);

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }
    }
}
