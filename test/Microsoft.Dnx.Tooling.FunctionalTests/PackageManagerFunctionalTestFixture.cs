// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    public class PackageManagerFunctionalTestFixture : DnxRuntimeFixture
    {
        private readonly DisposableDir _contextDir;

        public PackageManagerFunctionalTestFixture() : base()
        {
            _contextDir = TestUtils.CreateTempDir();
            PackageSource = Path.Combine(_contextDir.DirPath, "packages");
            Directory.CreateDirectory(PackageSource);

            CreateNewPackage("alpha", "0.1.0");

            PackAndInstallPackage(Path.Combine(TestUtils.GetMiscProjectsFolder(), "XreTestApps", "CommandsProject"), PackageSource);
        }

        public string PackageSource { get; }

        private void PackAndInstallPackage(string app, string packagesDir)
        {
            var runtimeForPacking = TestUtils.GetClrRuntimeComponents().FirstOrDefault();
            if (runtimeForPacking == null)
            {
                throw new InvalidOperationException("Can't find a CLR runtime to pack test packages.");
            }

            var runtimeHomePath = base.GetRuntimeHomeDir((string)runtimeForPacking[0],
                                                         (string)runtimeForPacking[1],
                                                         (string)runtimeForPacking[2]);
            var env = new Dictionary<string, string>
            {
                { EnvironmentNames.Packages, PackageSource }
            };

            using (var tempDir = new DisposableDir())
            {
                DnuTestUtils.ExecDnu(runtimeHomePath, "restore", app, env);
                DnuTestUtils.ExecDnu(runtimeHomePath, "pack", $"{app} --out {tempDir} --configuration Debug", env);
                var nupkgPath = Directory.EnumerateFiles(Path.Combine(tempDir, "Debug"))
                    .Where(f => f.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                    .First();
                DnuTestUtils.ExecDnu(runtimeHomePath, "packages add", $"{nupkgPath}", env);
            }
        }

        private void CreateNewPackage(string name, string version)
        {
            var projectJson = $@"{{
  ""version"": ""{version}"",
  ""frameworks"": {{
    ""dnx451"": {{ }},
    ""dnxcore50"": {{
      ""dependencies"": {{
          ""System.Runtime"": ""4.0.21-beta-*""
      }}
    }}
  }}
}}";

            var runtimeForPacking = TestUtils.GetClrRuntimeComponents().FirstOrDefault();
            if (runtimeForPacking == null)
            {
                throw new InvalidOperationException("Can't find a CLR runtime to pack test packages.");
            }

            var runtimeHomePath = base.GetRuntimeHomeDir((string)runtimeForPacking[0],
                                                         (string)runtimeForPacking[1],
                                                         (string)runtimeForPacking[2]);

            using (var tempdir = TestUtils.CreateTempDir())
            {
                var dir = new DirectoryInfo(tempdir);
                var projectDir = dir.CreateSubdirectory(name);
                var outputDir = dir.CreateSubdirectory("output");
                var projectJsonPath = Path.Combine(projectDir.FullName, "project.json");
                File.WriteAllText(projectJsonPath, projectJson);
                var env = new Dictionary<string, string>
                {
                    { EnvironmentNames.Packages, PackageSource }
                };

                DnuTestUtils.ExecDnu(runtimeHomePath, $"restore", $"{projectJsonPath} -s {PackageFeeds.AspNetvNextv2Feed}", env);
                DnuTestUtils.ExecDnu(runtimeHomePath, "pack", projectJsonPath + " --out " + outputDir.FullName, env);

                var packageName = string.Format("{0}.{1}.nupkg", name, version);
                var packageFile = Path.Combine(outputDir.FullName, "Debug", packageName);

                DnuTestUtils.ExecDnu(runtimeHomePath, "packages add", packageFile, env);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            _contextDir.Dispose();
        }
    }
}