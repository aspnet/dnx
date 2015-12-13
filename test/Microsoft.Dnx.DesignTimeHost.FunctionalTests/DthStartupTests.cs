// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Testing.Framework;
using Microsoft.Dnx.Testing.Framework.DesignTimeHost;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests
{
    public class DthStartupTests : DnxSdkFunctionalTestBase
    {
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

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DthStartup_GetProjectInformation(DnxSdk sdk)
        {
            using (var server = sdk.Dth.CreateServer())
            using (var client = new DthTestClient(server))
            {
                var solution = TestProjectsRepository.EnsureRestoredSolution("DthTestProjects");
                var project = solution.GetProject("EmptyConsoleApp");

                sdk.Dnu.Restore(project).EnsureSuccess();

                client.Initialize(project.ProjectDirectory);

                var projectInformation = client.DrainTillFirst("ProjectInformation")
                                               .EnsureSource(server, client)
                                               .RetrievePayloadAs<JObject>()
                                               .AssertProperty("Name", project.Name);

                projectInformation.RetrievePropertyAs<JArray>("Configurations")
                                  .AssertJArrayCount(2)
                                  .AssertJArrayContains("Debug")
                                  .AssertJArrayContains("Release");

                var frameworkShortNames = projectInformation.RetrievePropertyAs<JArray>("Frameworks")
                                                            .AssertJArrayCount(2)
                                                            .Select(f => f["ShortName"].Value<string>());

                Assert.Contains("dnxcore50", frameworkShortNames);
                Assert.Contains("dnx451", frameworkShortNames);

                TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(ProtocolNegotiationTestData))]
        public void DthStartup_ProtocolNegotiation(DnxSdk sdk, int requestVersion, int expectVersion)
        {
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                client.SetProtocolVersion(requestVersion);

                var response = client.DrainTillFirst(ProtocolManager.NegotiationMessageTypeName);
                response.EnsureSource(server, client);

                Assert.Equal(expectVersion, response.Payload["Version"]?.Value<int>());
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DthStartup_ProtocolNegotiation_ZeroIsNoAllowed(DnxSdk sdk)
        {
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                client.SetProtocolVersion(0);

                Assert.Throws<TimeoutException>(() =>
                {
                    client.DrainTillFirst(ProtocolManager.NegotiationMessageTypeName, timeout: TimeSpan.FromSeconds(1));
                });
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(RuntimeComponentsWithBothVersions))]
        public void DthCompilation_GetDiagnostics_OnEmptyConsoleApp(DnxSdk sdk, int protocolVersion)
        {
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                var solution = TestProjectsRepository.EnsureRestoredSolution("DthTestProjects");
                var project = solution.GetProject("EmptyConsoleApp");

                // Drain the inital messages
                client.Initialize(project.ProjectDirectory, protocolVersion);
                client.SendPayLoad(project, "GetDiagnostics");

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

                TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(RuntimeComponentsWithBothVersions))]
        public void DthCompilation_RestoreComplete_OnEmptyLibrary(DnxSdk sdk, int protocolVersion)
        {
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                var solution = TestUtils.GetSolution<DthStartupTests>(sdk, "DthTestProjects");
                var project = solution.GetProject("EmptyLibrary");

                sdk.Dnu.Restore(project).EnsureSuccess();

                // Drain the inital messages
                client.Initialize(project.ProjectDirectory, protocolVersion);

                client.DrainTillFirst("Dependencies")
                      .EnsureSource(server, client)
                      .EnsureNotContainDependency("System.Console");

                File.Copy(Path.Combine(project.ProjectDirectory, "project-update.json"),
                          Path.Combine(project.ProjectDirectory, "project.json"),
                          overwrite: true);

                sdk.Dnu.Restore(project).EnsureSuccess();

                client.SendPayLoad(project, "RestoreComplete");

                client.DrainTillFirst("Dependencies")
                       .EnsureSource(server, client)
                       .RetrieveDependency("System.Console");

                TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(UnresolvedDependencyTestData))]
        public void DthCompilation_Initialize_UnresolvedDependency(DnxSdk sdk, int protocolVersion, string referenceType, string testProjectName,
                                                                   string expectedUnresolvedDependency, string expectedUnresolvedType)
        {
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                var solution = TestProjectsRepository.EnsureRestoredSolution("DthTestProjects");
                var project = solution.GetProject(testProjectName);

                client.Initialize(project.ProjectDirectory, protocolVersion);

                var unresolveDependency = client.DrainTillFirst("Dependencies")
                                                .EnsureSource(server, client)
                                                .RetrieveDependency(expectedUnresolvedDependency);

                unresolveDependency.AssertProperty("Name", expectedUnresolvedDependency)
                                   .AssertProperty("DisplayName", expectedUnresolvedDependency)
                                   .AssertProperty("Resolved", false)
                                   .AssertProperty("Type", expectedUnresolvedType);

                if (expectedUnresolvedType == "Project")
                {
                    unresolveDependency.AssertProperty("Path", Path.Combine(Path.GetDirectoryName(project.ProjectDirectory),
                                                                            expectedUnresolvedDependency,
                                                                            Project.ProjectFileName));
                }
                else
                {
                    Assert.False(unresolveDependency["Path"].HasValues);
                }

                var referencesMessage = client.DrainTillFirst("References")
                                              .EnsureSource(server, client);

                if (referenceType == "Project")
                {
                    var expectedUnresolvedProjectPath = Path.Combine(Path.GetDirectoryName(project.ProjectDirectory),
                                                                     expectedUnresolvedDependency,
                                                                     Project.ProjectFileName);

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

                TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DthNegative_BrokenProjectPathInLockFile_V1(DnxSdk sdk)
        {
            var projectName = "BrokenProjectPathSample";
            var project = TestProjectsRepository.EnsureRestoredSolution("DthTestProjects")
                                                .GetProject(projectName);

            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                // After restore the project is copied to another place so that
                // the relative path in project lock file is invalid.
                var movedProjectPath = Path.Combine(TestUtils.GetTempTestFolder<DthStartupTests>(sdk), projectName);
                TestUtils.CopyFolder(project.ProjectDirectory, movedProjectPath);

                client.Initialize(movedProjectPath, protocolVersion: 1);

                var error = client.DrainTillFirst("DependencyDiagnostics")
                                  .RetrieveDependencyDiagnosticsCollection()
                                  .RetrieveDependencyDiagnosticsErrorAt<JValue>(0);

                Assert.Contains("error NU1002", error.Value<string>());

                client.DrainTillFirst("Dependencies")
                      .RetrieveDependency("EmptyLibrary")
                      .AssertProperty("Name", "EmptyLibrary")
                      .AssertProperty("Resolved", false);

                TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DthNegative_BrokenProjectPathInLockFile_V2(DnxSdk sdk)
        {
            var projectName = "BrokenProjectPathSample";
            var project = TestProjectsRepository.EnsureRestoredSolution("DthTestProjects")
                                                .GetProject(projectName);

            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                // After restore the project is copied to another place so that
                // the relative path in project lock file is invalid.
                var movedProjectPath = Path.Combine(TestUtils.GetTempTestFolder<DthStartupTests>(sdk), projectName);
                TestUtils.CopyFolder(project.ProjectDirectory, movedProjectPath);

                client.Initialize(movedProjectPath, protocolVersion: 2);

                client.DrainTillFirst("DependencyDiagnostics")
                      .RetrieveDependencyDiagnosticsCollection()
                      .RetrieveDependencyDiagnosticsErrorAt(0)
                      .AssertProperty<string>("FormattedMessage", message => message.Contains("error NU1002"))
                      .RetrievePropertyAs<JObject>("Source")
                      .AssertProperty("Name", "EmptyLibrary");

                client.DrainTillFirst("Dependencies")
                      .RetrieveDependency("EmptyLibrary")
                      .AssertProperty<JArray>("Errors", errorsArray => errorsArray.Count == 1)
                      .AssertProperty<JArray>("Warnings", warningsArray => warningsArray.Count == 0)
                      .AssertProperty("Name", "EmptyLibrary")
                      .AssertProperty("Resolved", false);
            }

            TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DthDependencies_UpdateGlobalJson_RefreshDependencies(DnxSdk sdk)
        {
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                var solution = TestUtils.GetSolution<DthStartupTests>(sdk, "DthUpdateSearchPathSample");

                var root = Path.Combine(solution.RootPath, "home");
                sdk.Dnu.Restore(root).EnsureSuccess();

                var testProject = Path.Combine(root, "src", "MainProject");

                client.Initialize(testProject, protocolVersion: 2);

                client.DrainTillFirst("ProjectInformation")
                      .RetrievePayloadAs<JObject>()
                      .RetrievePropertyAs<JArray>("ProjectSearchPaths")
                      .AssertJArrayCount(2);

                client.DrainTillFirst("DependencyDiagnostics")
                      .RetrievePayloadAs<JObject>()
                      .AssertProperty<JArray>("Errors", array => array.Count == 0)
                      .AssertProperty<JArray>("Warnings", array => array.Count == 0);

                client.DrainTillFirst("Dependencies")
                      .RetrieveDependency("Newtonsoft.Json")
                      .AssertProperty("Type", "Project")
                      .AssertProperty("Resolved", true)
                      .AssertProperty<JArray>("Errors", array => array.Count == 0, _ => "Dependency shouldn't contain any error.");

                // Overwrite the global.json to remove search path to ext
                File.WriteAllText(
                    Path.Combine(root, GlobalSettings.GlobalFileName),
                    JsonConvert.SerializeObject(new { project = new string[] { "src" } }));

                client.SendPayLoad(testProject, "RefreshDependencies");

                client.DrainTillFirst("ProjectInformation")
                      .RetrievePayloadAs<JObject>()
                      .RetrievePropertyAs<JArray>("ProjectSearchPaths")
                      .AssertJArrayCount(1)
                      .AssertJArrayElement(0, Path.Combine(root, "src"));

                client.DrainTillFirst("DependencyDiagnostics")
                      .RetrieveDependencyDiagnosticsCollection()
                      .RetrieveDependencyDiagnosticsErrorAt<JObject>(0)
                      .AssertProperty("ErrorCode", "NU1010");

                client.DrainTillFirst("Dependencies")
                      .RetrieveDependency("Newtonsoft.Json")
                      .AssertProperty("Type", LibraryTypes.Unresolved)
                      .AssertProperty("Resolved", false)
                      .RetrievePropertyAs<JArray>("Errors")
                      .AssertJArrayCount(1)
                      .RetrieveArraryElementAs<JObject>(0)
                      .AssertProperty("ErrorCode", "NU1010");

                TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DthCompilation_ChangeConfiguration(DnxSdk sdk)
        {
            // arrange
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                var project = TestProjectsRepository.EnsureRestoredSolution("DthTestProjects")
                                                    .GetProject("FailReleaseProject");

                var contextId = client.Initialize(project.ProjectDirectory);
                client.SendPayLoad(contextId, DthMessageTypes.GetDiagnostics);

                // the default configuration must be debug. therefore the sample project
                // can be compiled successfully
                client.DrainTillFirst(DthMessageTypes.AllDiagnostics)
                      .RetrieveCompilationDiagnostics("dnxcore50")
                      .RetrievePropertyAs<JArray>("Errors")
                      .AssertJArrayEmpty();

                client.SendPayLoad(contextId, DthMessageTypes.ChangeConfiguration, new
                {
                    Configuration = "Release"
                });

                client.SendPayLoad(contextId, DthMessageTypes.GetDiagnostics);

                client.DrainTillFirst(DthMessageTypes.AllDiagnostics)
                      .RetrieveCompilationDiagnostics("dnxcore50")
                      .RetrievePropertyAs<JArray>("Errors")
                      .AssertJArrayNotEmpty();
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DthStartup_EnumerateProjectContexts(DnxSdk sdk)
        {
            // arrange
            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                var solution = TestProjectsRepository.EnsureRestoredSolution("DthTestProjects");
                var projects = new Project[]
                {
                    solution.GetProject("EmptyLibrary"),
                    solution.GetProject("UnresolvedPackageSample"),
                    solution.GetProject("EmptyConsoleApp")
                };

                var contexts = projects.ToDictionary(proj => proj.ProjectDirectory,
                                                     proj => client.Initialize(proj.ProjectDirectory));

                // 7 response for each project initalization
                client.DrainMessage(21);

                // the context id here doesn't matter, this request is processed before it reaches
                // ApplicationContext
                client.SendPayLoad(1, DthMessageTypes.EnumerateProjectContexts, new { Version = 1 });

                var message = client.DrainTillFirst(DthMessageTypes.ProjectContexts);
                Assert.Equal(contexts.Count, message.Projects.Count);
                Assert.True(Enumerable.SequenceEqual(contexts, message.Projects));
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void CompileModuleWithDeps(DnxSdk sdk)
        {
            // Arrange
            var solution = TestProjectsRepository.EnsureRestoredSolution("CompileModuleWithDependencies");
            var project = solution.GetProject("A");

            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                client.Initialize(project.ProjectDirectory);

                client.SendPayLoad(project, "GetDiagnostics");

                // Assert
                var diagnosticsPerFramework = client.DrainTillFirst("AllDiagnostics")
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

            TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void AddDepsReturnsReferences(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DthStartupTests>(sdk, "HelloWorld");
            var project = solution.GetProject("HelloWorld");

            using (var server = sdk.Dth.CreateServer())
            using (var client = server.CreateClient())
            {
                sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

                client.SetProtocolVersion(3);

                client.Initialize(project.ProjectDirectory);

                // for a project supports two frameworks, 13 responses will be sent.
                // one ProjectInformation, and Depenedencies, DependencyDiagnostics,
                // References, Source, Diagnostics, CompilerOptions for each framework
                var messages = client.DrainMessage(13);

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

                messages = client.DrainMessage(4);

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

                var diagnosticsPerFramework = client.DrainTillFirst("AllDiagnostics")
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

            TestUtils.CleanUpTestDir<DthStartupTests>(sdk);
        }
    }
}
