// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Framework.CommonTestUtils;

namespace Microsoft.Framework.PackageManager.FunctionalTests
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

            PackPackage(Path.Combine(TestUtils.GetMiscProjectsFolder(), "XreTestApps/CommandsProject"), PackageSource);
        }

        public string PackageSource { get; }

        private void PackPackage(string app, string outpath)
        {
            var runtimeForPacking = TestUtils.GetClrRuntimeComponents().FirstOrDefault();
            if (runtimeForPacking == null)
            {
                throw new InvalidOperationException("Can't find a CLR runtime to pack test packages.");
            }

            var runtimeHomePath = base.GetRuntimeHomeDir((string)runtimeForPacking[0],
                                                         (string)runtimeForPacking[1],
                                                         (string)runtimeForPacking[2]);

            DnuTestUtils.ExecDnu(runtimeHomePath, "restore", "", environment: null, workingDir: app);
            DnuTestUtils.ExecDnu(runtimeHomePath, "pack", $"--out {outpath}", environment: null, workingDir: app);
        }

        private void CreateNewPackage(string name, string version)
        {
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
                var projectJson = Path.Combine(projectDir.FullName, "project.json");

                File.WriteAllText(projectJson, "{\"version\": \"" + version + "\"}");
                DnuTestUtils.ExecDnu(runtimeHomePath, "pack", projectJson + " --out " + outputDir.FullName, environment: null, workingDir: null);

                var packageName = string.Format("{0}.{1}.nupkg", name, version);
                var packageFile = Path.Combine(outputDir.FullName, "Debug", packageName);

                File.Copy(packageFile, Path.Combine(PackageSource, packageName), overwrite: true);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            _contextDir.Dispose();
        }
    }
}