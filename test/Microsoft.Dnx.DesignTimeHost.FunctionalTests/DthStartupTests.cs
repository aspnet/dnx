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

        public static IEnumerable<object[]> RuntimeComponentsWithBothVersions
        {
            get
            {
                foreach (var combination in RuntimeComponents)
                {
                    yield return combination.Concat(new object[] { 2 }).ToArray();
                    yield return combination.Concat(new object[] { 3 }).ToArray();
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
        [MemberData(nameof(RuntimeComponentsWithBothVersions))]
        public void DthCompilation_GetDiagnostics_OnEmptyConsoleApp(string flavor, string os, string architecture, int protocolVersion)
        {
            var projectName = "EmptyConsoleApp";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                // Drain the inital messages
                client.Initialize(testProject, protocolVersion);
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
        [MemberData(nameof(RuntimeComponentsWithBothVersions))]
        public void DthCompilation_RestoreComplete_OnEmptyLibrary(string flavor, string os, string architecture, int protocolVersion)
        {
            var projectName = "EmptyLibrary";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string testProject;
            using (_fixture.CreateDisposableTestProject(projectName, runtimeHomePath, out testProject))
            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                // Drain the inital messages
                client.Initialize(testProject, protocolVersion);

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
                    yield return combination.Concat(new object[] { 1, "Project", "UnresolvedProjectSample", "EmptyLibrary", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 1, "Package", "UnresolvedPackageSample", "NoSuchPackage", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 1, "Package", "IncompatiblePackageSample", "Newtonsoft.Json", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 2, "Project", "UnresolvedProjectSample", "EmptyLibrary", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 2, "Package", "UnresolvedPackageSample", "NoSuchPackage", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 2, "Package", "IncompatiblePackageSample", "Newtonsoft.Json", "Unresolved" }).ToArray();

                    yield return combination.Concat(new object[] { 3, "Project", "UnresolvedProjectSample", "EmptyLibrary", "Project" }).ToArray();

                    // Unresolved package dependency's type is still Unresolved
                    yield return combination.Concat(new object[] { 3, "Package", "UnresolvedPackageSample", "NoSuchPackage", "Unresolved" }).ToArray();

                    // Incompatible package's type, however, is Package
                    yield return combination.Concat(new object[] { 3, "Package", "IncompatiblePackageSample", "Newtonsoft.Json", "Package" }).ToArray();
                }
            }
        }

        [Theory]
        [MemberData(nameof(UnresolvedDependencyTestData))]
        public void DthCompilation_Initialize_UnresolvedDependency(
            string flavor, string os, string architecture, int protocolVersion, string referenceType,
            string testProjectName, string expectedUnresolvedDependency, string expectedUnresolvedType)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath(testProjectName);

            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                client.Initialize(testProject, protocolVersion);

                var dependenciesMessage = client.DrainTillFirst("Dependencies");
                dependenciesMessage.EnsureSource(server, client);

                var dependencies = dependenciesMessage.Payload["Dependencies"];
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

                var referencesMessage = client.DrainTillFirst("References");
                referencesMessage.EnsureSource(server, client);

                if (referenceType == "Project")
                {
                    var projectReferences = (JArray)referencesMessage.Payload["ProjectReferences"];
                    Assert.NotNull(projectReferences);

                    var projectReference = (JObject)projectReferences.Single();
                    var expectedUnresolvedProjectPath = Path.Combine(Path.GetDirectoryName(testProject), expectedUnresolvedDependency, Project.ProjectFileName);

                    Assert.Equal(expectedUnresolvedDependency, projectReference["Name"]);
                    Assert.Equal(expectedUnresolvedProjectPath, projectReference["Path"]);
                    Assert.False(projectReference["WrappedProjectPath"].HasValues);
                }
                else if (referenceType == "Package")
                {
                    var projectReferences = (JArray)referencesMessage.Payload["ProjectReferences"];
                    Assert.NotNull(projectReferences);
                    Assert.False(projectReferences.Any());
                }
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DthNegative_BrokenProjectPathInLockFile_V1(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath("BrokenProjectPathSample");

            using (var disposableDir = new DisposableDir())
            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                // copy the project to difference location so that the project path in its lock file is invalid
                var targetPath = Path.Combine(disposableDir, "BrokenProjectPathSample");
                TestUtils.CopyFolder(testProject, targetPath);

                client.Initialize(targetPath, protocolVersion: 1);
                var messages = client.DrainAllMessages();

                Assert.False(ContainsMessage(messages, "Error"));

                var dependencyDiagnosticsMessage = RetrieveSingle(messages, "DependencyDiagnostics");
                dependencyDiagnosticsMessage.EnsureSource(server, client);
                var errors = (JArray)dependencyDiagnosticsMessage.Payload["Errors"];
                Assert.Equal(1, errors.Count);
                Assert.Contains("error NU1001: The dependency EmptyLibrary  could not be resolved.", errors[0].Value<string>());

                var dependenciesMessage = RetrieveSingle(messages, "Dependencies");
                dependenciesMessage.EnsureSource(server, client);
                var dependency = dependenciesMessage.Payload["Dependencies"]["EmptyLibrary"] as JObject;
                Assert.NotNull(dependency);
                Assert.Equal("EmptyLibrary", dependency["Name"].Value<string>());
                Assert.False(dependency["Resolved"].Value<bool>());
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DthNegative_BrokenProjectPathInLockFile_V2(string flavor, string os, string architecture)
        {
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            var testProject = _fixture.GetTestProjectPath("BrokenProjectPathSample");

            using (var disposableDir = new DisposableDir())
            using (var server = DthTestServer.Create(runtimeHomePath, testProject))
            using (var client = new DthTestClient(server, 0))
            {
                // copy the project to difference location so that the project path in its lock file is invalid
                var targetPath = Path.Combine(disposableDir, "BrokenProjectPathSample");
                TestUtils.CopyFolder(testProject, targetPath);

                client.Initialize(targetPath, protocolVersion: 2);
                var messages = client.DrainAllMessages();

                Assert.False(ContainsMessage(messages, "Error"));

                var dependencyDiagnosticsMessage = RetrieveSingle(messages, "DependencyDiagnostics");
                dependencyDiagnosticsMessage.EnsureSource(server, client);
                var errors = (JArray)dependencyDiagnosticsMessage.Payload["Errors"];
                Assert.Equal(1, errors.Count);

                var formattedMessage = errors[0]["FormattedMessage"];
                Assert.NotNull(formattedMessage);
                Assert.Contains("error NU1001: The dependency EmptyLibrary  could not be resolved.", formattedMessage.Value<string>());

                var source = errors[0]["Source"] as JObject;
                Assert.NotNull(source);
                Assert.Equal("EmptyLibrary", source["Name"].Value<string>());

                var dependenciesMessage = RetrieveSingle(messages, "Dependencies");
                dependenciesMessage.EnsureSource(server, client);
                var dependency = dependenciesMessage.Payload["Dependencies"]["EmptyLibrary"] as JObject;
                Assert.NotNull(dependency);
                Assert.Equal("EmptyLibrary", dependency["Name"].Value<string>());
                Assert.False(dependency["Resolved"].Value<bool>());

                var dependencyErrors = dependency["Errors"] as JArray;
                Assert.NotNull(dependencyErrors);
                Assert.Equal(1, dependencyErrors.Count);

                var dependencyWarnings = dependency["Warnings"] as JArray;
                Assert.NotNull(dependencyWarnings);
                Assert.Equal(0, dependencyWarnings.Count);
            }
        }

        private bool ContainsMessage(IEnumerable<DthMessage> messages, string typename)
        {
            return messages.FirstOrDefault(msg => string.Equals(msg.MessageType, typename, StringComparison.Ordinal)) != null;
        }

        private DthMessage RetrieveSingle(IEnumerable<DthMessage> messages, string typename)
        {
            var result = messages.SingleOrDefault(msg => string.Equals(msg.MessageType, typename, StringComparison.Ordinal));

            if (result == null)
            {
                if (ContainsMessage(messages, typename))
                {
                    Assert.False(true, $"More than one {typename} messages exist.");
                }
                else
                {
                    Assert.False(true, $"{typename} message doesn't exists.");
                }
            }

            return result;
        }
    }
}
