// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure;
using Microsoft.Dnx.DesignTimeHost.FunctionalTests.Util;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests
{
    [Collection(nameof(DthFunctionalTestCollection))]
    public class DthStartupTests : DnxSdkFunctionalTestBase
    {
        private readonly DthFunctionalTestFixture _fixture;

        public DthStartupTests(DthFunctionalTestFixture fixture)
        {
            _fixture = fixture;
        }

        public static IEnumerable<object[]> RuntimeComponentsWithBothVersions
        {
            get
            {
                foreach (var combination in DnxSdks)
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
                foreach (var combination in DnxSdks)
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

        public static IEnumerable<object[]> UnresolvedDependencyTestData
        {
            get
            {
                foreach (var combination in DnxSdks)
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
        [MemberData(nameof(DnxSdks))]
        public void DthStartup_GetProjectInformation(DnxSdk sdk)
        {
            var projectName = "EmptyConsoleApp";
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(sdk))
            using (var client = new DthTestClient(server))
            {
                client.Initialize(testProject);

                var projectInformation = client.DrainAllMessages()
                                               .RetrieveSingleMessage("ProjectInformation")
                                               .EnsureSource(server, client)
                                               .RetrievePayloadAs<JObject>()
                                               .AssertProperty("Name", projectName);

                projectInformation.RetrievePropertyAs<JArray>("Configurations")
                                  .AssertJArrayCount(2)
                                  .AssertJArrayContains("Debug")
                                  .AssertJArrayContains("Release");

                var frameworkShortNames = projectInformation.RetrievePropertyAs<JArray>("Frameworks")
                                                            .AssertJArrayCount(2)
                                                            .Select(f => f["ShortName"].Value<string>());

                Assert.Contains("dnxcore50", frameworkShortNames);
                Assert.Contains("dnx451", frameworkShortNames);
            }
        }

        [Theory]
        [MemberData(nameof(ProtocolNegotiationTestData))]
        public void DthStartup_ProtocolNegotiation(DnxSdk sdk, int requestVersion, int expectVersion)
        {
            var projectName = "EmptyConsoleApp";
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                client.SetProtocolVersion(requestVersion);

                var response = client.DrainTillFirst(ProtocolManager.NegotiationMessageTypeName);
                response.EnsureSource(server, client);

                Assert.Equal(expectVersion, response.Payload["Version"]?.Value<int>());
            }
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void DthStartup_ProtocolNegotiation_ZeroIsNoAllowed(DnxSdk sdk)
        {
            var projectName = "EmptyConsoleApp";
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
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
        public void DthCompilation_GetDiagnostics_OnEmptyConsoleApp(DnxSdk sdk, int protocolVersion)
        {
            var projectName = "EmptyConsoleApp";
            var testProject = _fixture.GetTestProjectPath(projectName);

            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                // Drain the inital messages
                client.Initialize(testProject, protocolVersion);
                client.SendPayLoad(testProject, "GetDiagnostics");

                var diagnosticsGroup = client.DrainTillFirst("AllDiagnostics")
                                             .EnsureSource(server, client)
                                             .RetrievePayloadAs<JArray>()
                                             .AssertJArrayCount(3);

                foreach (var group in diagnosticsGroup)
                {
                    group.AsJObject()
                         .AssertProperty<JArray>("Errors", errorsArray => !errorsArray.Any())
                         .AssertProperty<JArray>("Warnings", warningsArray => !warningsArray.Any());
                }
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponentsWithBothVersions))]
        public void DthCompilation_RestoreComplete_OnEmptyLibrary(DnxSdk sdk, int protocolVersion)
        {
            var projectName = "EmptyLibrary";

            string testProject;
            using (_fixture.CreateDisposableTestProject(projectName, sdk, out testProject))
            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                // Drain the inital messages
                client.Initialize(testProject, protocolVersion);

                client.DrainTillFirst("Dependencies")
                      .EnsureSource(server, client)
                      .EnsureNotContainDependency("System.Console");

                File.Copy(Path.Combine(testProject, "project-update.json"),
                          Path.Combine(testProject, "project.json"),
                          overwrite: true);

                sdk.Dnu.Restore(testProject).EnsureSuccess();

                client.SendPayLoad(testProject, "RestoreComplete");

                client.DrainTillFirst("Dependencies")
                       .EnsureSource(server, client)
                       .RetrieveDependency("System.Console");
            }
        }

        [Theory]
        [MemberData(nameof(UnresolvedDependencyTestData))]
        public void DthCompilation_Initialize_UnresolvedDependency(DnxSdk sdk, int protocolVersion, string referenceType, string testProjectName,
                                                                   string expectedUnresolvedDependency, string expectedUnresolvedType)
        {
            var testProject = _fixture.GetTestProjectPath(testProjectName);

            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                client.Initialize(testProject, protocolVersion);

                var messages = client.DrainAllMessages();

                var unresolveDependency = messages.RetrieveSingleMessage("Dependencies")
                                                  .EnsureSource(server, client)
                                                  .RetrieveDependency(expectedUnresolvedDependency);
                unresolveDependency.AssertProperty("Name", expectedUnresolvedDependency)
                                   .AssertProperty("DisplayName", expectedUnresolvedDependency)
                                   .AssertProperty("Resolved", false)
                                   .AssertProperty("Type", expectedUnresolvedType);

                if (expectedUnresolvedType == "Project")
                {
                    unresolveDependency.AssertProperty(
                        "Path",
                        Path.Combine(Path.GetDirectoryName(testProject), expectedUnresolvedDependency, Project.ProjectFileName));
                }
                else
                {
                    Assert.False(unresolveDependency["Path"].HasValues);
                }

                var referencesMessage = messages.RetrieveSingleMessage("References")
                                                .EnsureSource(server, client);

                if (referenceType == "Project")
                {
                    var expectedUnresolvedProjectPath = Path.Combine(Path.GetDirectoryName(testProject), expectedUnresolvedDependency, Project.ProjectFileName);

                    referencesMessage.RetrievePayloadAs<JObject>()
                                     .RetrievePropertyAs<JArray>("ProjectReferences")
                                     .AssertJArrayCount(1)
                                     .RetrieveArraryElementAs<JObject>(0)
                                     .AssertProperty("Name", expectedUnresolvedDependency)
                                     .AssertProperty("Path", expectedUnresolvedProjectPath)
                                     .AssertProperty<JToken>("WrappedProjectPath", prop => !prop.HasValues);
                }
                else if (referenceType == "Package")
                {
                    referencesMessage.RetrievePayloadAs<JObject>()
                                     .RetrievePropertyAs<JArray>("ProjectReferences")
                                     .AssertJArrayCount(0);
                }
            }
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void DthNegative_BrokenProjectPathInLockFile_V1(DnxSdk sdk)
        {
            var testProject = _fixture.GetTestProjectPath("BrokenProjectPathSample");

            using (var disposableDir = new DisposableDir())
            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                // copy the project to difference location so that the project path in its lock file is invalid
                var targetPath = Path.Combine(disposableDir, "BrokenProjectPathSample");
                Testing.TestUtils.CopyFolder(testProject, targetPath);

                client.Initialize(targetPath, protocolVersion: 1);
                var messages = client.DrainAllMessages()
                                     .AssertDoesNotContain("Error");

                var error = messages.RetrieveSingleMessage("DependencyDiagnostics")
                                    .RetrieveDependencyDiagnosticsCollection()
                                    .RetrieveDependencyDiagnosticsErrorAt<JValue>(0);

                Assert.Contains("error NU1001: The dependency EmptyLibrary  could not be resolved.", error.Value<string>());

                messages.RetrieveSingleMessage("Dependencies")
                        .RetrieveDependency("EmptyLibrary")
                        .AssertProperty("Name", "EmptyLibrary")
                        .AssertProperty("Resolved", false);
            }
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void DthNegative_BrokenProjectPathInLockFile_V2(DnxSdk sdk)
        {
            var testProject = _fixture.GetTestProjectPath("BrokenProjectPathSample");

            using (var disposableDir = new DisposableDir())
            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                // copy the project to difference location so that the project path in its lock file is invalid
                var targetPath = Path.Combine(disposableDir, "BrokenProjectPathSample");
                Testing.TestUtils.CopyFolder(testProject, targetPath);

                client.Initialize(targetPath, protocolVersion: 2);
                var messages = client.DrainAllMessages()
                                     .AssertDoesNotContain("Error");

                messages.RetrieveSingleMessage("DependencyDiagnostics")
                        .RetrieveDependencyDiagnosticsCollection()
                        .RetrieveDependencyDiagnosticsErrorAt(0)
                        .AssertProperty<string>("FormattedMessage", message => message.Contains("error NU1001: The dependency EmptyLibrary  could not be resolved."))
                        .RetrievePropertyAs<JObject>("Source")
                        .AssertProperty("Name", "EmptyLibrary");

                messages.RetrieveSingleMessage("Dependencies")
                        .RetrieveDependency("EmptyLibrary")
                        .AssertProperty<JArray>("Errors", errorsArray => errorsArray.Count == 1)
                        .AssertProperty<JArray>("Warnings", warningsArray => warningsArray.Count == 0)
                        .AssertProperty("Name", "EmptyLibrary")
                        .AssertProperty("Resolved", false);
            }
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void DthDependencies_UpdateGlobalJson_RefreshDependencies(DnxSdk sdk)
        {
            using (var disposableDir = new DisposableDir())
            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                Testing.TestUtils.CopyFolder(
                    _fixture.GetTestProjectPath("UpdateSearchPathSample"),
                    Path.Combine(disposableDir, "UpdateSearchPathSample"));

                var root = Path.Combine(disposableDir, "UpdateSearchPathSample", "home");
                sdk.Dnu.Restore(root).EnsureSuccess();

                var testProject = Path.Combine(root, "src", "MainProject");

                client.Initialize(testProject, protocolVersion: 2);
                var messages = client.DrainAllMessages();

                messages.RetrieveSingleMessage("ProjectInformation")
                        .RetrievePayloadAs<JObject>()
                        .RetrievePropertyAs<JArray>("ProjectSearchPaths")
                        .AssertJArrayCount(2);

                messages.RetrieveSingleMessage("Dependencies")
                        .RetrieveDependency("Newtonsoft.Json")
                        .AssertProperty("Type", "Project")
                        .AssertProperty("Resolved", true)
                        .AssertProperty<JArray>("Errors", array => array.Count == 0, _ => "Dependency shouldn't contain any error.");

                messages.RetrieveSingleMessage("DependencyDiagnostics")
                        .RetrievePayloadAs<JObject>()
                        .AssertProperty<JArray>("Errors", array => array.Count == 0)
                        .AssertProperty<JArray>("Warnings", array => array.Count == 0);

                // Overwrite the global.json to remove search path to ext
                File.WriteAllText(
                    Path.Combine(root, GlobalSettings.GlobalFileName),
                    JsonConvert.SerializeObject(new { project = new string[] { "src" } }));

                client.SendPayLoad(testProject, "RefreshDependencies");
                messages = client.DrainAllMessages();

                messages.RetrieveSingleMessage("ProjectInformation")
                        .RetrievePayloadAs<JObject>()
                        .RetrievePropertyAs<JArray>("ProjectSearchPaths")
                        .AssertJArrayCount(1)
                        .AssertJArrayElement(0, Path.Combine(root, "src"));

                messages.RetrieveSingleMessage("Dependencies")
                        .RetrieveDependency("Newtonsoft.Json")
                        .AssertProperty("Type", LibraryTypes.Unresolved)
                        .AssertProperty("Resolved", false)
                        .RetrievePropertyAs<JArray>("Errors")
                        .AssertJArrayCount(1)
                        .RetrieveArraryElementAs<JObject>(0)
                        .AssertProperty("ErrorCode", "NU1010");

                messages.RetrieveSingleMessage("DependencyDiagnostics")
                        .RetrieveDependencyDiagnosticsCollection()
                        .RetrieveDependencyDiagnosticsErrorAt<JObject>(0)
                        .AssertProperty("ErrorCode", "NU1010");
            }
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void CompileModuleWithDeps(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DthStartupTests>(sdk, "CompileModuleWithDependencies");
            var project = solution.GetProject("A");

            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

                client.Initialize(project.ProjectDirectory);

                client.SendPayLoad(project, "GetDiagnostics");

                var messages = client.DrainAllMessages();


                // Assert
                messages.AssertDoesNotContain("Error");

                var diagnosticsPerFramework = messages.RetrieveSingleMessage("AllDiagnostics")
                                                      .RetrievePayloadAs<JArray>()
                                                      .AssertJArrayCount(3);

                foreach (var frameworkDiagnostics in diagnosticsPerFramework)
                {
                    var errors = frameworkDiagnostics.Value<JArray>("Errors");
                    var warnings = frameworkDiagnostics.Value<JArray>("Warnings");
                    Assert.Equal(0, errors.Count);
                    Assert.Equal(0, warnings.Count);
                }
            }
        }

        [Theory]
        [MemberData(nameof(DnxSdks))]
        public void AddDepsReturnsReferences(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DthStartupTests>(sdk, "HelloWorld");
            var project = solution.GetProject("HelloWorld");

            using (var server = DthTestServer.Create(sdk))
            using (var client = server.CreateClient())
            {
                sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

                client.SetProtocolVersion(3);

                client.Initialize(project.ProjectDirectory);

                var messages = client.DrainAllMessages();

                foreach (var frameworkInfo in project.GetTargetFrameworks())
                {
                    var messagesByFramework = messages.GetMessagesByFramework(frameworkInfo.FrameworkName);

                    messagesByFramework.RetrieveSingleMessage("Dependencies")
                                       .RetrieveDependency("Newtonsoft.Json")
                                       .AssertProperty("Type", LibraryTypes.Package);

                    var references = messagesByFramework.RetrieveSingleMessage("References")
                                                        .RetrievePayloadAs<JObject>()
                                                        .RetrievePropertyAs<JArray>("FileReferences");

                    Assert.NotEmpty(references);
                    Assert.Contains("Newtonsoft.Json", references.Select(r => Path.GetFileNameWithoutExtension(r.Value<string>())));
                }

                // Update dependencies
                project = project.UpdateProjectFile(json =>
                {
                    json["dependencies"]["DoesNotExist"] = "1.0.0";
                });

                client.SendPayLoad(project, "FilesChanged");

                messages = client.DrainAllMessages();

                foreach (var frameworkInfo in project.GetTargetFrameworks())
                {
                    var messagesByFramework = messages.GetMessagesByFramework(frameworkInfo.FrameworkName);

                    var dependencies = messagesByFramework.RetrieveSingleMessage("Dependencies");

                    dependencies.RetrieveDependency("Newtonsoft.Json")
                                .AssertProperty("Type", LibraryTypes.Package);

                    dependencies.RetrieveDependency("DoesNotExist")
                                .AssertProperty("Type", LibraryTypes.Unresolved);

                    // The references should not have changed
                    messagesByFramework.AssertDoesNotContain("References");
                }

                client.SendPayLoad(project, "GetDiagnostics");

                messages = client.DrainAllMessages();

                var diagnosticsPerFramework = messages.RetrieveSingleMessage("AllDiagnostics")
                                                      .RetrievePayloadAs<JArray>()
                                                      .AssertJArrayCount(3);

                foreach (var frameworkDiagnostics in diagnosticsPerFramework)
                {
                    if (!frameworkDiagnostics["Framework"].HasValues)
                    {
                        continue;
                    }

                    var errors = frameworkDiagnostics.Value<JArray>("Errors");
                    var warnings = frameworkDiagnostics.Value<JArray>("Warnings");
                    Assert.Equal(2, errors.Count);
                    Assert.Equal(0, warnings.Count);

                    var error1 = errors[0];
                    var error2 = errors[1];

                    Assert.Equal("NU1006", error1.Value<string>("ErrorCode"));
                    Assert.Equal("NU1001", error2.Value<string>("ErrorCode"));
                    Assert.Equal("DoesNotExist", error2["Source"].Value<string>("Name"));
                }
            }
        }
    }
}
