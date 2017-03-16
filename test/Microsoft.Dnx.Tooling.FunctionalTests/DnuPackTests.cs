// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Dnx.Testing.Framework;
using Newtonsoft.Json.Linq;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    public class DnuPackTests : DnxSdkFunctionalTestBase
    {
        [ConditionalTheory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        [OSSkipCondition(OperatingSystems.Linux)]
        [OSSkipCondition(OperatingSystems.MacOSX)]
        public void DnuPack_ClientProfile(DnxSdk sdk)
        {
            const string ProjectName = "ClientProfileProject";
            var projectStructure = new Dir
            {
                [ProjectName] = new Dir
                {
                    ["project.json"] = new JObject
                    {
                        ["frameworks"] = new JObject
                        {
                            ["net40-client"] = new JObject(),
                            ["net35-client"] = new JObject()
                        }
                    }
                },
                ["Output"] = new Dir()
            };

            var baseDir = TestUtils.GetTestFolder<DnuPackTests>(sdk);
            var projectDir = Path.Combine(baseDir, ProjectName);
            var outputDir = Path.Combine(baseDir, "Output");
            projectStructure.Save(baseDir);

            sdk.Dnu.Restore(projectDir).EnsureSuccess();
            var result = sdk.Dnu.Pack(projectDir, outputDir);

            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void CompileModuleWithDeps(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, "CompileModuleWithDependencies");
            var project = solution.GetProject("A");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void P2PDifferentFrameworks(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, "ProjectToProject");
            var project = solution.GetProject("P1");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void DnuPack_ResourcesNoArgs(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, Path.Combine("ResourcesTestProjects", "ReadFromResources"));

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            var project = solution.GetProject("ReadFromResources");

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(result.GetSateliteAssemblyPath(VersionUtility.ParseFrameworkName("dnx451"), "fr-FR")));
            Assert.True(File.Exists(result.GetSateliteAssemblyPath(VersionUtility.ParseFrameworkName("dnxcore50"), "fr-FR")));

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
         }
         
        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void ProjectPropertiesFlowIntoAssembly(DnxSdk sdk)
        {
            // Arrange
            var solution = TestUtils.GetSolution<DnuPackTests>(sdk, "AssemblyInfo");
            var project = solution.GetProject("Test");

            sdk.Dnu.Restore(solution.RootPath).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(project.ProjectDirectory);

            // Assert
            Assert.Equal(0, result.ExitCode);

            var assemblyPath = result.GetAssemblyPath(sdk.TargetFramework);

            using (var stream = File.OpenRead(assemblyPath))
            {
                using (var peReader = new PEReader(stream))
                {
                    var metadataReader = peReader.GetMetadataReader();
                    var assemblyDefinition = metadataReader.GetAssemblyDefinition();
                    var attributes = assemblyDefinition.GetCustomAttributes()
                        .Select(handle => metadataReader.GetCustomAttribute(handle))
                        .ToDictionary(attribute => GetAttributeName(attribute, metadataReader), attribute => GetAttributeArgument(attribute, metadataReader));

                    Assert.Equal(project.Title, attributes[typeof(AssemblyTitleAttribute).Name]);
                    Assert.Equal(project.Description, attributes[typeof(AssemblyDescriptionAttribute).Name]);
                    Assert.Equal(project.Copyright, attributes[typeof(AssemblyCopyrightAttribute).Name]);
                    Assert.Equal(project.AssemblyFileVersion.ToString(), attributes[typeof(AssemblyFileVersionAttribute).Name]);
                    Assert.Equal(project.Version.ToString(), attributes[typeof(AssemblyInformationalVersionAttribute).Name]);
                    Assert.Equal(project.Version.Version, assemblyDefinition.Version);
                }
            }

            TestUtils.CleanUpTestDir<DnuPackTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void BuildCanBeOverriddenByAScript(DnxSdk sdk)
        {
            var frameworks = new[]
            {
                VersionUtility.ParseFrameworkName("dnx451"),
                VersionUtility.ParseFrameworkName("dnxcore50")
            };

            // Arrange
            var projectStructure = new Dir
            {
                ["project.json"] = new JObject(
                    new JProperty("frameworks", new JObject(
                        frameworks.Select(f => new JProperty(VersionUtility.GetShortFrameworkName(f), new JObject())))),
                    new JProperty("scripts", new JObject(
                        new JProperty("prebuild", new JArray(
                            "echo 'Prebuild for %build:TargetFramework% > %build:OutputDirectory%/prebuild")),
                        new JProperty("postbuild", new JArray(
                            "echo 'Postbuild for %build:TargetFramework% > %build:OutputDirectory%/postbuild")),
                        new JProperty("build", new JArray(
                            "echo Building %build:TargetFramework% to %build:OutputDirectory%",
                            "rd /s /q %build:OutputDirectory%",
                            "mkdir %build:OutputDirectory%",
                            "echo 'Built for %build:TargetFramework%' > %build:OutputDirectory%/MyDll.dll")))))
            };
            var testDir = TestUtils.GetTestFolder<DnuPackTests>(sdk);
            projectStructure.Save(testDir);

            sdk.Dnu.Restore(testDir).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Build(testDir);

            // Assert
            foreach(var fx in frameworks)
            {
                var shortName = VersionUtility.GetShortFrameworkName(fx);
                var asm = result.GetAssemblyPath(fx);
                Assert.True(File.Exists(asm));
                Assert.Equal($"Built for {shortName}", File.ReadAllText(asm));

                var prebuild = result.GetOutputFilePath(fx, "prebuild");
                Assert.True(File.Exists(prebuild));
                Assert.Equal($"Prebuild for {shortName}", File.ReadAllText(prebuild));

                var postbuild = result.GetOutputFilePath(fx, "postbuild");
                Assert.True(File.Exists(postbuild));
                Assert.Equal($"Postbuild for {shortName}", File.ReadAllText(postbuild));
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void PackUsesOutputsProvidedByBuildScriptIfSpecified(DnxSdk sdk)
        {
            var frameworks = new[]
            {
                VersionUtility.ParseFrameworkName("dnx451"),
                VersionUtility.ParseFrameworkName("dnxcore50")
            };

            // Arrange
            var projectStructure = new Dir
            {
                ["project.json"] = new JObject(
                    new JProperty("frameworks", new JObject(
                        frameworks.Select(f => new JProperty(VersionUtility.GetShortFrameworkName(f), new JObject())))),
                    new JProperty("scripts", new JObject(
                        new JProperty("prebuild", new JArray(
                            "echo 'Prebuild for %build:TargetFramework% > %build:OutputDirectory%/prebuild")),
                        new JProperty("postbuild", new JArray(
                            "echo 'Postbuild for %build:TargetFramework% > %build:OutputDirectory%/postbuild")),
                        new JProperty("build", new JArray(
                            "echo Building %build:TargetFramework% to %build:OutputDirectory%",
                            "rd /s /q %build:OutputDirectory%",
                            "mkdir %build:OutputDirectory%",
                            "echo 'Built for %build:TargetFramework%' > %build:OutputDirectory%/MyDll.dll")))))
            };
            var testDir = TestUtils.GetTestFolder<DnuPackTests>(sdk);
            projectStructure.Save(testDir);

            sdk.Dnu.Restore(testDir).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Pack(testDir);

            // Open the output package
            using(var zip = ZipFile.OpenRead(result.PackagePath))
            {
                var pkg = new ZipPackage(result.PackagePath);
                
                foreach(var fx in frameworks)
                {
                    Assert.Contains(fx, pkg.GetSupportedFrameworks());

                    IEnumerable<IPackageAssemblyReference> asmRefs;
                    Assert.True(VersionUtility.GetNearest(fx, pkg.AssemblyReferences, out asmRefs));

                    var reference = asmRefs.Single();
                    string content;
                    using(var reader =new StreamReader(reference.GetStream()))
                    {
                        content = reader.ReadToEnd();
                    }
                    Assert.Equal($"Built for {VersionUtility.GetShortFrameworkName(fx)}", content);
                }
            }
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void PackCanBeOverriddenByAScript(DnxSdk sdk)
        {
            var frameworks = new[]
            {
                VersionUtility.ParseFrameworkName("dnx451"),
                VersionUtility.ParseFrameworkName("dnxcore50")
            };

            // Arrange
            var projectStructure = new Dir
            {
                ["project.json"] = new JObject(
                    new JProperty("frameworks", new JObject(
                        frameworks.Select(f => new JProperty(VersionUtility.GetShortFrameworkName(f), new JObject())))),
                    new JProperty("scripts", new JObject(
                        new JProperty("prepack", new JArray(
                            "echo 'Prepack for %build:TargetFramework% > %project:BuildOutputDir%/prepack")),
                        new JProperty("postpack", new JArray(
                            "echo 'Postpack for %build:TargetFramework% > %project:BuildOutputDir%/postpack")),
                        new JProperty("pack", new JArray(
                            "echo Packing to %project:BuildOutputDir%",
                            "rd /s /q %project:BuildOutputDir%",
                            "mkdir %project:BuildOutputDir%",
                            "echo 'Packed for %build:TargetFramework%' > %project:BuildOutputDir%/MyPackage.nupkg")))))
            };
            var testDir = TestUtils.GetTestFolder<DnuPackTests>(sdk);
            projectStructure.Save(testDir);

            sdk.Dnu.Restore(testDir).EnsureSuccess();

            // Act
            var result = sdk.Dnu.Build(testDir);

            // Assert
            foreach(var fx in frameworks)
            {
                var shortName = VersionUtility.GetShortFrameworkName(fx);
                var asm = result.GetAssemblyPath(fx);
                Assert.True(File.Exists(asm));
                Assert.Equal($"Packed for {shortName}", File.ReadAllText(asm));

                var prepack = result.GetOutputFilePath(fx, "prepack");
                Assert.True(File.Exists(prepack));
                Assert.Equal($"Prepack for {shortName}", File.ReadAllText(prepack));

                var postpack = result.GetOutputFilePath(fx, "postpack");
                Assert.True(File.Exists(postpack));
                Assert.Equal($"Postpack for {shortName}", File.ReadAllText(postpack));
            }
        }

        private string GetAttributeName(CustomAttribute attribute, MetadataReader metadataReader)
        {
            var container = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            var name = metadataReader.GetTypeReference((TypeReferenceHandle)container).Name;
            return metadataReader.GetString(name);
        }

        private string GetAttributeArgument(CustomAttribute attribute, MetadataReader metadataReader)
        {
            var signature = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Signature;
            var signatureReader = metadataReader.GetBlobReader(signature);
            var valueReader = metadataReader.GetBlobReader(attribute.Value);

            valueReader.ReadUInt16(); // Skip prolog
            signatureReader.ReadSignatureHeader(); // Skip header

            int parameterCount;
            signatureReader.TryReadCompressedInteger(out parameterCount);

            signatureReader.ReadSignatureTypeCode(); // Skip return type

            for (int i = 0; i < parameterCount; i++)
            {
                var signatureTypeCode = signatureReader.ReadSignatureTypeCode();
                if (signatureTypeCode == SignatureTypeCode.String)
                {
                    return valueReader.ReadSerializedString();
                }
            }

            return string.Empty;
        }
    }
}
