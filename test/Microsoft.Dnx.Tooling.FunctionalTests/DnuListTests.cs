// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    [Collection(nameof(PackageManagerFunctionalTestCollection))]
    public class DnuListTests : IDisposable
    {
        private readonly PackageManagerFunctionalTestFixture _fixture;
        private readonly DisposableDir _workingDir;

        public DnuListTests(PackageManagerFunctionalTestFixture fixture)
        {
            _fixture = fixture;
            _workingDir = TestUtils.CreateTempDir();
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
        [MemberData(nameof(RuntimeComponents))]
        public void DnuList_EmptyProject_Default(string flavor, string os, string architecture)
        {
            string stdOut, stdErr;
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var expectedTitle = string.Format(
                @"Listing dependencies for {0} ({1})",
                Path.GetFileName(_workingDir.DirPath),
                Path.Combine(_workingDir, "project.json"));

            CreateProjectJson(@"{}");

            // run dnu restore first
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "restore", _workingDir.DirPath));

            // run dnu list
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            // assert
            Assert.True(string.IsNullOrEmpty(stdErr));
            Assert.True(stdOut.Contains(expectedTitle));
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuList_EmptyProject_Details(string flavor, string os, string architecture)
        {
            string stdOut, stdErr;
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var expectedTitle = string.Format(
                @"Listing dependencies for {0} ({1})",
                Path.GetFileName(_workingDir.DirPath),
                Path.Combine(_workingDir, "project.json"));

            CreateProjectJson(@"{}");

            // run dnu restore first
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "restore", _workingDir.DirPath));

            // run dnu list
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "--details", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            // assert
            Assert.True(string.IsNullOrEmpty(stdErr));
            Assert.True(stdOut.Contains(expectedTitle));
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuList_SingleDependencyProject(string flavor, string os, string architecture)
        {
            string stdOut, stdErr;
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            CreateProjectJson(new
            {
                dependencies = new
                {
                    alpha = "0.1.0"
                },
                frameworks = new
                {
                    dnx451 = new { },
                    dnxcore50 = new { }
                }
            });

            // restore the packages
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "restore", "--source " + _fixture.PackageSource, workingDir: _workingDir.DirPath));

            // run dnu list
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            // there should be 2 and only 2 dependencies of alpha
            var resolvedHits = stdOut.Split('\n').Where(line => line.Contains("* alpha 0.1.0"))
                                         .Where(line => !line.Contains("Unresolved"));
            var unresolvedHits = stdOut.Split('\n').Where(line => line.Contains("* alpha 0.1.0"))
                                         .Where(line => line.Contains("Unresolved"));
            Assert.Equal(1, resolvedHits.Count());
            Assert.Equal(1, unresolvedHits.Count());
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuList_SingleDependencyProject_Detailed(string flavor, string os, string architecture)
        {
            string stdOut, stdErr;
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var projectName = Path.GetFileName(_workingDir.DirPath);

            CreateProjectJson(new
            {
                dependencies = new
                {
                    alpha = "0.1.0"
                },
                frameworks = new
                {
                    dnx451 = new { }
                }
            });

            // restore the packages
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "restore", "--source " + _fixture.PackageSource, workingDir: _workingDir.DirPath));

            // run dnu list
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "--details", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            // assert - in the output of the dnu list, alpha 0.1.0 is listed as resolved, its source is listed on the second line.
            string[] outputLines = stdOut.Split(Environment.NewLine[0]);
            Assert.True(outputLines.Length > 0);
            for (int i = 0; i < outputLines.Length; ++i)
            {
                if (outputLines[i].Contains("* alpha 0.1.0"))
                {
                    Assert.False(outputLines[i].Contains("Unresolved"), "Dnu list reports unresolved package");

                    // the following line should list the dependency's source
                    Assert.True(++i < outputLines.Length);
                    Assert.True(outputLines[i].Contains(projectName));
                }
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuList_Unresolved(string flavor, string os, string architecture)
        {
            string stdOut, stdErr;
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var projectName = Path.GetFileName(_workingDir.DirPath);

            CreateProjectJson(new
            {
                dependencies = new
                {
                    alpha = "0.1.0",
                    beta = "0.2.0"
                },
                frameworks = new
                {
                    dnx451 = new { },
                    dnxcore50 = new { }
                }
            });

            // restore the packages, it should fail because missing package beta
            Assert.Equal(1, DnuTestUtils.ExecDnu(runtimeHomePath, "restore", "--source " + _fixture.PackageSource, workingDir: _workingDir.DirPath));

            // run dnu list
            Assert.Equal(0, DnuTestUtils.ExecDnu(runtimeHomePath, "list", "", out stdOut, out stdErr, environment: null, workingDir: _workingDir.DirPath));

            // the beta package is not resolved
            var hits = SplitLines(stdOut).Where(line => line.Contains("* beta 0.2.0 - Unresolved"));
            Assert.Equal(2, hits.Count());
        }

        private string[] SplitLines(string content)
        {
            return content.Split(Environment.NewLine[0]);
        }

        private void CreateProjectJson(string content)
        {
            var projectJson = Path.Combine(_workingDir.DirPath, "project.json");
            File.WriteAllText(projectJson, content);
        }

        private void CreateProjectJson(object content)
        {
            CreateProjectJson(JsonConvert.SerializeObject(content));
        }
    }
}