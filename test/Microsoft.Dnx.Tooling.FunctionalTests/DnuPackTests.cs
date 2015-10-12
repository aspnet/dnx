// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.AspNet.Testing.xunit;
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
