// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.FunctionalTestUtils;

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

            Console.WriteLine("[{0}] context directory {1}", nameof(DnuListTestContext), _contextDir.DirPath);
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
            Console.WriteLine("[{0}:{1}] Create package {2}", nameof(DnuListTestContext), nameof(CreateNewPackage), name);

            var runtimeHomePath = GetRuntimeHome("clr", "win", "x86");

            using (var tempdir = TestUtils.CreateTempDir())
            {
                var dir = new DirectoryInfo(tempdir);
                var projectDir = dir.CreateSubdirectory(name);
                var outputDir = dir.CreateSubdirectory("output");
                var projectJson = Path.Combine(projectDir.FullName, "project.json");

                File.WriteAllText(projectJson, @"{
    ""version"": ""_version_""
}".Replace("_version_", version));
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