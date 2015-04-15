// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.CommonTestUtils;

namespace Microsoft.Framework.PackageManager.FunctionalTests
{
    public class DnuListTestContext : IDisposable
    {
        private readonly IDictionary<Tuple<string, string, string>, DisposableDir> _runtimeHomeDirs =
            new Dictionary<Tuple<string, string, string>, DisposableDir>();

        private readonly DisposableDir _contextDir;

        public DnuListTestContext()
        {
            _contextDir = TestUtils.CreateTempDir();
            PackageSource = Path.Combine(_contextDir.DirPath, "packages");
            Directory.CreateDirectory(PackageSource);

            CreateNewPackage("alpha", "0.1.0");
        }

        public string GetRuntimeHome(string flavor, string os, string architecture)
        {
            DisposableDir result;
            if (!_runtimeHomeDirs.TryGetValue(Tuple.Create(flavor, os, architecture), out result))
            {
                result = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
                _runtimeHomeDirs.Add(Tuple.Create(flavor, os, architecture), result);
            }

            return result.DirPath;
        }

        public string PackageSource { get; }

        private void CreateNewPackage(string name, string version)
        {
            var runtimeForPacking = TestUtils.GetClrRuntimeComponents().FirstOrDefault();
            if (runtimeForPacking == null)
            {
                throw new InvalidOperationException("Can't find a CLR runtime to pack test packages.");
            }

            var runtimeHomePath = GetRuntimeHome((string)runtimeForPacking[0],
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

        public void Dispose()
        {
            foreach (var each in _runtimeHomeDirs.Values)
            {
                each.Dispose();
            }

            _contextDir.Dispose();
        }
    }
}