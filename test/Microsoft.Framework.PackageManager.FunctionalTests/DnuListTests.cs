// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.FunctionalTestUtils;
using Xunit;

namespace Microsoft.Framework.PackageManager.FunctionalTests
{
    public class DnuListTests : IClassFixture<DnuTestContext>, IDisposable
    {
        private readonly DnuTestContext _context;
        private readonly DisposableDir _workingDir;

        public DnuListTests(DnuTestContext context)
        {
            _context = context;
        }

        public static IEnumerable<object[]> RuntimeComponents
        {
            get { return TestUtils.GetRuntimeComponentsCombinations(); }
        }

        public void Dispose()
        {
            _workingDir.Dispose();
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuList_EmptyProject_Default(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _context.GetRuntimeHome(flavor, os, architecture);

            var projectJson = Path.Combine(_workingDir.DirPath, "project.json");
            File.WriteAllText(projectJson, @"{}");

            string stdOut, stdErr;
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuList_EmptyProject_Details(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _context.GetRuntimeHome(flavor, os, architecture);
            var projectJson = Path.Combine(_workingDir.DirPath, "project.json");
            File.WriteAllText(projectJson, @"{}");

            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "--details", environment: null, workingDir: _workingDir.DirPath));
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuList_SingleDependencyProject(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _context.GetRuntimeHome(flavor, os, architecture);
            var projectJson = Path.Combine(_workingDir.DirPath, "project.json");
            File.WriteAllText(projectJson, @"{
  ""dependencies"": {
    ""alpha"": ""0.1.0""
  },
  ""frameworks"": {
    ""dnx451"": {},
    ""dnxcore50"": {}
  }
}");

            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "restore", "--source " + _context.PackageSource, workingDir: _workingDir.DirPath));

            string stdOut, stdErr;
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            var hits = stdOut.Split('\n').Where(line => line.Contains("* alpha 0.1.0"))
                                         .Where(line => !line.Contains("Unresolved"));
            Assert.Equal(2, hits.Count());
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuList_SingleDependencyProject_Detailed(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _context.GetRuntimeHome(flavor, os, architecture);
            var projectJson = Path.Combine(_workingDir.DirPath, "project.json");
            File.WriteAllText(projectJson, @"{
  ""dependencies"": {
    ""alpha"": ""0.1.0""
  },
  ""frameworks"": {
    ""dnx451"": {},
    ""dnxcore50"": {}
  }
}");

            var projectName = Path.GetFileName(_workingDir.DirPath);

            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "restore", "--source " + _context.PackageSource, workingDir: _workingDir.DirPath));

            string[] stdOut, stdErr;
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "--details", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            Console.WriteLine(string.Join("\n", stdOut));
            Console.WriteLine(string.Join("\n", stdErr));

            for (int i = 0; i < stdOut.Length; ++i)
            {
                if (stdOut[i].Contains("* alpha 0.1.0"))
                {
                    Assert.False(stdOut[i].Contains("Unresolved"), "Dnu list reports unresolved package");
                    Assert.True(stdOut[i + 1].Contains(projectName));
                }
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuList_Unresolved(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _context.GetRuntimeHome(flavor, os, architecture);
            var projectJson = Path.Combine(_workingDir.DirPath, "project.json");
            File.WriteAllText(projectJson, @"{
  ""dependencies"": {
    ""alpha"": ""0.1.0""
  },
  ""frameworks"": {
    ""dnx451"": {},
    ""dnxcore50"": {}
  }
}");

            var projectName = Path.GetFileName(_workingDir.DirPath);

            string stdOut, stdErr;
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            var hits = stdOut.Split('\n').Where(line => line.Contains("* alpha 0.1.0") && line.Contains("Unresolved"));
            Assert.Equal(2, hits.Count());
        }
    }

    public class DnuTestContext : IDisposable
    {
        private readonly IDictionary<Tuple<string, string, string>, DisposableDir> _runtimeHomeDirs =
            new Dictionary<Tuple<string, string, string>, DisposableDir>();

        private readonly DisposableDir _contextDir;

        public DnuTestContext()
        {
            _contextDir = TestUtils.CreateTempDir();
            PackageSource = Path.Combine(_contextDir.DirPath, "packages");
            Directory.CreateDirectory(PackageSource);

            CreateNewPackage("alpha", "0.1.0");

            Console.WriteLine("[{0}] context directory {1}", nameof(DnuTestContext), _contextDir.DirPath);
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
            Console.WriteLine("[{0}:{1}] Create package {2}", nameof(DnuTestContext), nameof(CreateNewPackage), name);

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