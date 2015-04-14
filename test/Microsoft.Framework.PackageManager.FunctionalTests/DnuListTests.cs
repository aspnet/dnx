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
    public class DnuListTests : IClassFixture<DnuListTestContext>, IDisposable
    {
        private readonly DnuListTestContext _context;
        private readonly DisposableDir _workingDir;

        public DnuListTests(DnuListTestContext context)
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

            string stdOut, stdErr;
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "--details", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            string[] outputLines = stdOut.Split(Environment.NewLine[0]);

            for (int i = 0; i < outputLines.Length; ++i)
            {
                if (outputLines[i].Contains("* alpha 0.1.0"))
                {
                    Assert.False(outputLines[i].Contains("Unresolved"), "Dnu list reports unresolved package");
                    Assert.True(outputLines[i + 1].Contains(projectName));
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
}