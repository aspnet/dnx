// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Tooling;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests
{
    [Collection(nameof(DthFunctionalTestCollection))]
    public class DthStartupTests
    {
        private readonly DthFunctionalTestFixture _fixture;

        public DthStartupTests(DthFunctionalTestFixture fixture)
        {
            _fixture = fixture;
        }

        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                foreach (var runtime in TestUtils.GetClrRuntimeComponents())
                {
                    yield return runtime;
                }

                if (!RuntimeEnvironmentHelper.IsMono)
                {
                    foreach (var runtime in TestUtils.GetCoreClrRuntimeComponents())
                    {
                        yield return runtime;
                    }
                }
            }
        }

        public static IEnumerable<object[]> ProtocolNegotiationTestData
        {
            get
            {
                foreach (var combination in RuntimeComponents)
                {
                    // The current max protocol version is 3

                    // request 1, respond 1
                    yield return combination.Concat(new object[] { 1, 1 }).ToArray();
                    // request 2, respond 2
                    yield return combination.Concat(new object[] { 2, 2 }).ToArray();
                    // request 3, respond 2
                    yield return combination.Concat(new object[] { 3, 3 }).ToArray();
                    // request 4, respond 3
                    yield return combination.Concat(new object[] { 4, 3 }).ToArray();
                }
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DthStartup_GetProjectInformation(string flavor, string os, string architecture)
        {
            var projectName = "EmptyConsoleApp";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                client.Initialize(testProject);

                var response = client.DrainTillFirst("ProjectInformation");
                response.EnsureSource(server, client);

                var projectInfo = response.Payload;
                Assert.Equal(projectName, projectInfo["Name"]);
                Assert.Equal(2, projectInfo["Configurations"].Count());
                Assert.Contains("Debug", projectInfo["Configurations"]);
                Assert.Contains("Release", projectInfo["Configurations"]);

                var frameworkShorNames = projectInfo["Frameworks"].Select(f => f["ShortName"].Value<string>());
                Assert.Equal(2, frameworkShorNames.Count());
                Assert.Contains("dnxcore50", frameworkShorNames);
                Assert.Contains("dnx451", frameworkShorNames);
            }
        }

        [Theory]
        [MemberData(nameof(ProtocolNegotiationTestData))]
        public void DthStartup_ProtocolNegotiation(string flavor, string os, string architecture, int requestVersion, int expectVersion)
        {
            var projectName = "EmptyConsoleApp";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                client.SetProtocolVersion(requestVersion);

                var response = client.DrainTillFirst(ProtocolManager.NegotiationMessageTypeName);
                response.EnsureSource(server, client);

                Assert.Equal(expectVersion, response.Payload["Version"]?.Value<int>());
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DthStartup_ProtocolNegotiation_ZeroIsNoAllowed(string flavor, string os, string architecture)
        {
            var projectName = "EmptyConsoleApp";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                client.SetProtocolVersion(0);

                Assert.Throws<TimeoutException>(() =>
                {
                    client.DrainTillFirst(ProtocolManager.NegotiationMessageTypeName);
                });
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DthCompilation_GetDiagnostics_OnEmptyConsoleApp(string flavor, string os, string architecture)
        {
            var projectName = "EmptyConsoleApp";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                // Drain the inital messages
                client.Initialize(testProject);
                client.SendPayLoad("GetDiagnostics");

                var message = client.DrainTillFirst("AllDiagnostics");
                message.EnsureSource(server, client);
                var payload = (message.Payload as JArray)?.OfType<JObject>();
                Assert.NotNull(payload);
                Assert.Equal(3, payload.Count());

                foreach (var df in payload)
                {
                    var errors = (JArray)df["Errors"];
                    Assert.False(errors.Any(), $"Unexpected compilation errors {string.Join(", ", errors.Select(e => e.Value<string>()))}");

                    var warnings = (JArray)df["Warnings"];
                    Assert.False(warnings.Any(), $"Unexpected compilation warnings {string.Join(", ", warnings.Select(e => e.Value<string>()))}");
                }
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DthCompilation_RestoreComplete_OnEmptyLibrary(string flavor, string os, string architecture)
        {
            var projectName = "EmptyLibrary";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string testProject;
            using (_fixture.CreateDisposableTestProject(projectName, runtimeHomePath, out testProject))
            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                // Drain the inital messages
                client.Initialize(testProject);

                var before = client.DrainTillFirst("Dependencies");
                before.EnsureSource(server, client);
                Assert.Null(before.Payload["Dependencies"]["System.Console"]);

                File.Copy(Path.Combine(testProject, "project-update.json"),
                          Path.Combine(testProject, "project.json"),
                          overwrite: true);

                string stdOut, stdErr;
                var restoreExitCode = DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "restore",
                    arguments: testProject,
                    stdOut: out stdOut,
                    stdErr: out stdErr);
                Assert.Equal(0, restoreExitCode);

                client.SendPayLoad("RestoreComplete");

                var after = client.DrainTillFirst("Dependencies");
                after.EnsureSource(server, client);
                Assert.NotNull(after.Payload["Dependencies"]["System.Console"]);
            }
        }

        public static IEnumerable<object[]> UnresolvedDependencyTestData
        {
            get
            {
                foreach (var combination in RuntimeComponents)
                {
                    yield return combination.Concat(new object[] { 1, "UnresolvedProjectSample", "EmptyLibrary", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 1, "UnresolvedPackageSample", "NoSuchPackage", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 2, "UnresolvedProjectSample", "EmptyLibrary", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 2, "UnresolvedPackageSample", "NoSuchPackage", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 3, "UnresolvedProjectSample", "EmptyLibrary", "Project" }).ToArray();

                    // Unresolved package dependency's type is still Unresolved
                    yield return combination.Concat(new object[] { 3, "UnresolvedPackageSample", "NoSuchPackage", "Unresolved" }).ToArray();
                }
            }
        }

        [Theory]
        [MemberData(nameof(UnresolvedDependencyTestData))]
        public void DthCompilation_Initialize_UnresolvedDependency(
            string flavor, string os, string architecture, int protocolVersion,
            string testProjectName, string expectedUnresolvedDependency, string expectedUnresolvedType)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath(testProjectName);

            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                client.Initialize(testProject, protocolVersion);

                var message = client.DrainTillFirst("Dependencies");
                message.EnsureSource(server, client);

                var dependencies = message.Payload["Dependencies"];
                Assert.NotNull(dependencies);

                var unresolveDependency = dependencies[expectedUnresolvedDependency];
                Assert.NotNull(unresolveDependency);

                Assert.Equal(expectedUnresolvedDependency, unresolveDependency["Name"]);
                Assert.Equal(expectedUnresolvedDependency, unresolveDependency["DisplayName"]);
                Assert.False(unresolveDependency["Resolved"].Value<bool>());
                Assert.Equal(expectedUnresolvedType, unresolveDependency["Type"].Value<string>());

                if (expectedUnresolvedType == "Project")
                {
                    Assert.Equal(
                        Path.Combine(Path.GetDirectoryName(testProject), expectedUnresolvedDependency, Project.ProjectFileName),
                        unresolveDependency["Path"].Value<string>());
                }
                else
                {
                    Assert.False(unresolveDependency["Path"].HasValues);
                }
            }
        }
    }
}
