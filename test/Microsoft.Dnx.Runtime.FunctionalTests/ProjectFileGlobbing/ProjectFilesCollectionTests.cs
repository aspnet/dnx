// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.FunctionalTests.Utilities;
using Microsoft.Dnx.Runtime.Json;
using Xunit;

namespace Microsoft.Dnx.Runtime.FunctionalTests.ProjectFileGlobbing
{
    public class ProjectFilesCollectionTests : FileGlobbingTestBase
    {
        public ProjectFilesCollectionTests()
            : base()
        {
        }

        [Fact]
        public void DefaultSearchPathForSources()
        {
            var testFilesCollection = CreateFilesCollection(@"{}", "src\\project");
            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\sub2\source4.cs",
                @"src\project\sub2\source5.cs");
        }

        [Fact]
        public void SearchPathForSourcesWithExtraSettings()
        {
            var testFilesCollection = CreateFilesCollection(@"
{ 
    ""compileExclude"": ""**/*4.cs"",
    ""compileFiles"": ""../project2/sub/source2.cs""
}
", "src\\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\sub2\source5.cs",
                @"src\project2\sub\source2.cs");
        }

        [Fact]
        public void CompileRespecifyEquivalence()
        {
            var defaultCollection = CreateFilesCollection(@"
{ 
    ""compile"": ""**\\*.txt""
}
", "src\\project2");

            var respecifyCollection = CreateFilesCollection(@"
{
    ""compile"": ""**\\*.cs;**\\*.txt""
}
", "src\\project2");

            VerifyFilePathsCollection(defaultCollection.SourceFiles, respecifyCollection.SourceFiles.ToArray());
        }

        [Theory]
        [InlineData(@"{""compile"": [""**\\*.cs""]}")]
        [InlineData(@"{""compile"": [""**/*.cs""]}")]
        [InlineData(@"{""compileBuiltIn"":"""", ""compile"": [""**/*.cs""]}")]
        [InlineData(@"{""compileBuiltIn"":"""", ""compile"": [""**\\*.cs""]}")]
        [InlineData(@"{""compileBuiltIn"":""**\\*.cs"", ""compile"": [""**/*.cs""]}")]
        [InlineData(@"{""compileBuiltIn"":""**/*.cs"", ""compile"": [""**/*.cs""]}")]
        [InlineData(@"{""compileBuiltIn"":""**\\*.cs"", ""compile"": [""**\\*.cs""]}")]
        [InlineData(@"{""compileBuiltIn"":""**/*.cs"", ""compile"": [""**\\*.cs""]}")]
        public void CompileRespecifyInArrayEquivalence(string projectJsonContent)
        {
            var defaultCollection = CreateFilesCollection(@"
{ 
}
", "src\\project2");

            var respecifyCollection = CreateFilesCollection(projectJsonContent, "src/project2");

            VerifyFilePathsCollection(defaultCollection.SourceFiles, respecifyCollection.SourceFiles.ToArray());
        }

        [Fact]
        public void MutlipleCompileFilesInclude()
        {

            var testFilesCollection = CreateFilesCollection(@"
{ 
    ""compileBuiltIn"": """",
    ""compile"": """",
    ""compileExclude"": """",
    ""compileFiles"": ""source1.cs;sub\\source2.cs""
}
", "src\\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs");
        }

        [Fact]
        public void MutlipleCompileFilesIncludeInArrary()
        {

            var testFilesCollection = CreateFilesCollection(@"
{ 
    ""compileBuiltIn"": """",
    ""compile"": """",
    ""compileExclude"": """",
    ""compileFiles"": [""source1.cs"", ""sub\\source2.cs""]
}
", "src\\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs");
        }

        [Fact]
        public void DefaultSearchPathForResources()
        {
            var testFilesCollection = CreateFilesCollection(@"{}", "src\\project");
            VerifyFilePathsCollection(testFilesCollection.ResourceFiles.Keys,
                @"src\project\compiler\resources\resource.res",
                @"src\project\compiler\resources\sub\resource2.res",
                @"src\project\compiler\resources\sub\sub\resource3.res");
        }

        [Fact]
        public void DefaultSearchPathForPreprocessSource()
        {
            var testFilesCollection = CreateFilesCollection(@"{}", "src\\project");
            VerifyFilePathsCollection(testFilesCollection.PreprocessSourceFiles,
                @"src\project\compiler\preprocess\preprocess-source1.cs",
                @"src\project\compiler\preprocess\sub\preprocess-source2.cs",
                @"src\project\compiler\preprocess\sub\sub\preprocess-source3.cs");
        }

        [Fact]
        public void DefaultSearchPathForShared()
        {
            var testFilesCollection = CreateFilesCollection(@"{}", "src\\project");
            VerifyFilePathsCollection(testFilesCollection.SharedFiles,
                @"src\project\compiler\shared\shared1.cs",
                @"src\project\compiler\shared\sub\shared2.cs",
                @"src\project\compiler\shared\sub\sub\sharedsub.cs");
        }

        [Fact]
        public void IncludeCodesUpperLevelWithSlash()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""../../lib/**/*.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\..\..\lib\source6.cs",
                @"src\project\..\..\lib\sub3\source7.cs",
                @"src\project\..\..\lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodesUpperLevelWithSlashLegacy()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""code"": ""../../lib/**/*.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\..\..\lib\source6.cs",
                @"src\project\..\..\lib\sub3\source7.cs",
                @"src\project\..\..\lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodesUpperLevel()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""..\\..\\lib\\**\\*.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\..\..\lib\source6.cs",
                @"src\project\..\..\lib\sub3\source7.cs",
                @"src\project\..\..\lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodesUsingUpperLevelAndRecursive()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""**\\*.cs;..\\..\\lib\\**\\*.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\..\..\lib\source6.cs",
                @"src\project\..\..\lib\sub3\source7.cs",
                @"src\project\..\..\lib\sub4\source8.cs",
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\sub2\source4.cs",
                @"src\project\sub2\source5.cs");
        }

        [Fact]
        public void IncludeCodesUsingUpperLevelAndRecursiveLegacy()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""code"": ""**\\*.cs;..\\..\\lib\\**\\*.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\..\..\lib\source6.cs",
                @"src\project\..\..\lib\sub3\source7.cs",
                @"src\project\..\..\lib\sub4\source8.cs",
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\sub2\source4.cs",
                @"src\project\sub2\source5.cs");
        }

        [Fact]
        public void IncludeCodesUsingUpperLevelAndWildcard()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""**\\*.cs;..\\..\\lib\\*.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\..\..\lib\source6.cs",
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\sub2\source4.cs",
                @"src\project\sub2\source5.cs");
        }

        [Fact]
        public void IncludeCodesUpperLevelSingleFile()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""..\\..\\lib\\sub4\\source8.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\..\..\lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodesUpperLevelSingleFileAndRecursive()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compile"": ""**\\*.cs;..\\..\\lib\\sub4\\source8.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\..\..\lib\sub4\source8.cs",
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\sub2\source4.cs",
                @"src\project\sub2\source5.cs");
        }

        [Fact]
        public void IncludeCodeFolder()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""sub/""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs");
        }

        [Fact]
        public void IncludeCodeFolderBackSlash()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""sub\\""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs");
        }

        [Fact]
        public void IncludeCodeUpperLevelFolder()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""..\\project2\\""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project2\content1.txt",
                @"src\project2\source1.cs",
                @"src\project2\bin\object",
                @"src\project2\compiler\preprocess\preprocess-source1.cs",
                @"src\project2\compiler\preprocess\sub\preprocess-source2.cs",
                @"src\project2\compiler\preprocess\sub\sub\preprocess-source3.cs",
                @"src\project2\compiler\preprocess\sub\sub\preprocess-source3.txt",
                @"src\project2\compiler\resources\resource.res",
                @"src\project2\compiler\resources\sub\resource2.res",
                @"src\project2\compiler\resources\sub\sub\resource3.res",
                @"src\project2\compiler\shared\shared1.cs",
                @"src\project2\compiler\shared\shared1.txt",
                @"src\project2\compiler\shared\sub\shared2.cs",
                @"src\project2\compiler\shared\sub\shared2.txt",
                @"src\project2\compiler\shared\sub\sub\sharedsub.cs",
                @"src\project2\obj\object.o",
                @"src\project2\sub\source2.cs",
                @"src\project2\sub\source3.cs",
                @"src\project2\sub2\source4.cs",
                @"src\project2\sub2\source5.cs");
        }

        [Fact]
        public void IncludeCodeFolderWithSlash()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""sub/""
}
", @"src\project");
            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs");
        }

        [Fact]
        public void IncludeCodeFolderWithoutSlash()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""sub""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs");
        }

        [Fact]
        public void ThrowForAbosolutePath()
        {
            var absolutePath = Path.Combine(Root.DirPath, @"source5.cs");
            var projectJsonContent = @"{""compile"": """ + absolutePath.Replace("\\", "\\\\") + @"""}";

            var exception = Assert.Throws<FileFormatException>(() =>
            {
                CreateFilesCollection(projectJsonContent, @"src\project");
            });

            Assert.Equal(exception.Message, "The 'compile' property cannot be a rooted path.");
        }

        [Fact]
        public void IncludeCodeCurrentDirectory()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": "".\\**\\*.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\source1.cs",
                @"src\project\sub2\source4.cs",
                @"src\project\sub2\source5.cs");
        }

        [Fact]
        public void IncludeCodeUpperLevelWithCurrent()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""..\\..\\lib\\.""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"lib\source6.cs",
                @"lib\sub3\source7.cs",
                @"lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodeUpperLevelFolderWithExcluding()
        {
            var testFilesCollection = CreateFilesCollection(@"
{
    ""compileBuiltIn"": """",
    ""compile"": ""..\\project2\\"",
    ""exclude"": ""..\\project2\\compiler\\**\\*.txt"",
    ""resources"": ""..\\project2\\compiler\\resources\\**\\*.*""
}
", @"src\project");

            VerifyFilePathsCollection(testFilesCollection.SourceFiles,
                @"src\project2\content1.txt",
                @"src\project2\source1.cs",
                @"src\project2\bin\object",
                @"src\project2\compiler\preprocess\preprocess-source1.cs",
                @"src\project2\compiler\preprocess\sub\preprocess-source2.cs",
                @"src\project2\compiler\preprocess\sub\sub\preprocess-source3.cs",
                @"src\project2\compiler\shared\shared1.cs",
                @"src\project2\compiler\shared\sub\shared2.cs",
                @"src\project2\compiler\shared\sub\sub\sharedsub.cs",
                @"src\project2\obj\object.o",
                @"src\project2\sub\source2.cs",
                @"src\project2\sub\source3.cs",
                @"src\project2\sub2\source4.cs",
                @"src\project2\sub2\source5.cs");
        }

        protected override void CreateContext()
        {
            AddFiles(
                    "src/project/source1.cs",
                    "src/project/sub/source2.cs",
                    "src/project/sub/source3.cs",
                    "src/project/sub2/source4.cs",
                    "src/project/sub2/source5.cs",
                    "src/project/compiler/preprocess/preprocess-source1.cs",
                    "src/project/compiler/preprocess/sub/preprocess-source2.cs",
                    "src/project/compiler/preprocess/sub/sub/preprocess-source3.cs",
                    "src/project/compiler/preprocess/sub/sub/preprocess-source3.txt",
                    "src/project/compiler/shared/shared1.cs",
                    "src/project/compiler/shared/shared1.txt",
                    "src/project/compiler/shared/sub/shared2.cs",
                    "src/project/compiler/shared/sub/shared2.txt",
                    "src/project/compiler/shared/sub/sub/sharedsub.cs",
                    "src/project/compiler/resources/resource.res",
                    "src/project/compiler/resources/sub/resource2.res",
                    "src/project/compiler/resources/sub/sub/resource3.res",
                    "src/project/content1.txt",
                    "src/project/obj/object.o",
                    "src/project/bin/object",
                    "src/project/.hidden/file1.hid",
                    "src/project/.hidden/sub/file2.hid",
                    "src/project2/source1.cs",
                    "src/project2/sub/source2.cs",
                    "src/project2/sub/source3.cs",
                    "src/project2/sub2/source4.cs",
                    "src/project2/sub2/source5.cs",
                    "src/project2/compiler/preprocess/preprocess-source1.cs",
                    "src/project2/compiler/preprocess/sub/preprocess-source2.cs",
                    "src/project2/compiler/preprocess/sub/sub/preprocess-source3.cs",
                    "src/project2/compiler/preprocess/sub/sub/preprocess-source3.txt",
                    "src/project2/compiler/shared/shared1.cs",
                    "src/project2/compiler/shared/shared1.txt",
                    "src/project2/compiler/shared/sub/shared2.cs",
                    "src/project2/compiler/shared/sub/shared2.txt",
                    "src/project2/compiler/shared/sub/sub/sharedsub.cs",
                    "src/project2/compiler/resources/resource.res",
                    "src/project2/compiler/resources/sub/resource2.res",
                    "src/project2/compiler/resources/sub/sub/resource3.res",
                    "src/project2/content1.txt",
                    "src/project2/obj/object.o",
                    "src/project2/bin/object",
                    "lib/source6.cs",
                    "lib/sub3/source7.cs",
                    "lib/sub4/source8.cs",
                    "res/resource1.text",
                    "res/resource2.text",
                    "res/resource3.text",
                    ".hidden/file1.hid",
                    ".hidden/sub/file2.hid");
        }

        protected override ProjectFilesCollection CreateFilesCollection(string jsonContent, string projectDir)
        {
            using (var reader = new StringReader(jsonContent))
            {
                var rawProject = JsonDeserializer.Deserialize(reader) as JsonObject;

                projectDir = Path.Combine(Root.DirPath, PathHelper.NormalizeSeparator(projectDir));
                var filesCollection = new ProjectFilesCollection(rawProject, projectDir, string.Empty);

                return filesCollection;
            }
        }
    }
}