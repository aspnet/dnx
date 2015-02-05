// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime.FunctionalTests.Utilities;
using Xunit;

namespace Microsoft.Framework.Runtime.FunctionalTests.ProjectFileGlobbing
{
    public class ProjectFileGlobbingFacts : IDisposable
    {
        protected DisposableProjectContext Context;

        public ProjectFileGlobbingFacts()
        {
            Context = CreateContext();
        }

        public void Dispose()
        {
            if (Context != null)
            {
                Context.Dispose();
            }
        }

        [Fact]
        public void DefaultSearchPathForSources()
        {
            var testProject = CreateTestProject(@"{}", "src\\project");
            VerifyFilePathsCollection(testProject.SourceFiles,
                @"src\project\source1.cs",
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\sub2\source4.cs",
                @"src\project\sub2\source5.cs");
        }

        [Fact]
        public void DefaultSearchPathForContents()
        {
            var testProject = CreateTestProject(@"{}", "src\\project");
            VerifyFilePathsCollection(testProject.ContentFiles,
                @"src\project\content1.txt",
                @"src\project\compiler\preprocess\sub\sub\preprocess-source3.txt",
                @"src\project\compiler\shared\shared1.txt",
                @"src\project\compiler\shared\sub\shared2.txt");
        }

        [Fact]
        public void DefaultSearchPathForResources()
        {
            var testProject = CreateTestProject(@"{}", "src\\project");
            VerifyFilePathsCollection(testProject.ResourceFiles,
                @"src\project\compiler\resources\resource.res",
                @"src\project\compiler\resources\sub\resource2.res",
                @"src\project\compiler\resources\sub\sub\resource3.res");
        }

        [Fact]
        public void DefaultSearchPathForPreprocessSource()
        {
            var testProject = CreateTestProject(@"{}", "src\\project");
            VerifyFilePathsCollection(testProject.PreprocessSourceFiles,
                @"src\project\compiler\preprocess\preprocess-source1.cs",
                @"src\project\compiler\preprocess\sub\preprocess-source2.cs",
                @"src\project\compiler\preprocess\sub\sub\preprocess-source3.cs");
        }

        [Fact]
        public void DefaultSearchPathForShared()
        {
            var testProject = CreateTestProject(@"{}", "src\\project");
            VerifyFilePathsCollection(testProject.SharedFiles,
                @"src\project\compiler\shared\shared1.cs",
                @"src\project\compiler\shared\sub\shared2.cs",
                @"src\project\compiler\shared\sub\sub\sharedsub.cs");
        }

        [Fact]
        public void DefaultSearchPathForBundleExcludeFiles()
        {
            var testProject = CreateTestProject(@"{}", "src\\project");
            VerifyFilePathsCollection(testProject.BundleExcludeFiles,
                @"src\project\bin\object",
                @"src\project\obj\object.o",
                @"src\project\.hidden/file1.hid",
                @"src\project\.hidden/sub/file2.hid");
        }

        [Fact]
        public void IncludeCodesUpperLevelWithSlash()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": ""../../lib/**/*.cs"",
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
                @"src\project\..\..\lib\source6.cs",
                @"src\project\..\..\lib\sub3\source7.cs",
                @"src\project\..\..\lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodesUpperLevel()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": ""..\\..\\lib\\**\\*.cs"",
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
                @"src\project\..\..\lib\source6.cs",
                @"src\project\..\..\lib\sub3\source7.cs",
                @"src\project\..\..\lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodesUsingUpperLevelAndRecursive()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": ""**\\*.cs;..\\..\\lib\\**\\*.cs"",
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
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
            var testProject = CreateTestProject(@"
{
    ""code"": ""**\\*.cs;..\\..\\lib\\*.cs"",
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
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
            var testProject = CreateTestProject(@"
{
    ""code"": ""..\\..\\lib\\sub4\\source8.cs"",
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
                @"src\project\..\..\lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodesUpperLevelSingleFileAndRecursive()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": ""**\\*.cs;..\\..\\lib\\sub4\\source8.cs"",
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
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
            var testProject = CreateTestProject(@"
{
    ""code"": ""sub\\""
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs");
        }

        [Fact]
        public void IncludeCodeUpperLevelFolder()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": ""..\\project2\\""
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
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
            var testProject = CreateTestProject(@"
{
    ""code"": ""sub/""
}
", @"src\project");
            VerifyFilePathsCollection(testProject.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs");
        }

        [Fact]
        public void IncludeCodeFolderWithoutSlash()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": ""sub""
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs");
        }

        [Fact]
        public void IncludeCodeAbsolutePath()
        {
            var absolutePath = Path.Combine(Context.RootPath, @"src\project2\sub2\source5.cs");
            var projectJsonContent = @"{""code"": """ + absolutePath.Replace("\\", "\\\\") + @"""}";
            var testProject = CreateTestProject(projectJsonContent, @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles, absolutePath);
        }

        [Fact(Skip = "https://github.com/aspnet/XRE/issues/1187")]
        public void IncludeCodeCurrentDirectory()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": "".\\**\\*.cs""
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
                @"src\project\sub\source2.cs",
                @"src\project\sub\source3.cs",
                @"src\project\source1.cs",
                @"src\project\sub2\source4.cs",
                @"src\project\sub2\source5.cs");
        }

        [Fact]
        public void IncludeCodeUpperLevelWithCurrent()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": ""..\\..\\lib\\.""
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
                @"lib\source6.cs",
                @"lib\sub3\source7.cs",
                @"lib\sub4\source8.cs");
        }

        [Fact]
        public void IncludeCodeUpperLevelFolderWithExcluding()
        {
            var testProject = CreateTestProject(@"
{
    ""code"": ""..\\project2\\"",
    ""exclude"": ""..\\project2\\compiler\\**\\*.txt"",
    ""resources"": ""..\\project2\\compiler\\resources\\**\\*.*""
}
", @"src\project");

            VerifyFilePathsCollection(testProject.SourceFiles,
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

        private DisposableProjectContext CreateContext()
        {
            var context = new DisposableProjectContext();
            context.AddFiles(
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

            return context;
        }

        private Project CreateTestProject(string projectJsonContent, string relativePath, string name = null)
        {
            return Project.GetProject(
                projectJsonContent,
                name ?? "testproject",
                Path.Combine(Context.RootPath, relativePath, "project.json"));
        }

        private void VerifyFilePathsCollection(IEnumerable<string> actualFiles, params string[] expectFiles)
        {
            var expectFilesInFullpath = expectFiles.Select(relativePath => Path.GetFullPath(Path.Combine(Context.RootPath, relativePath)));
            var actualFilesInFullpath = actualFiles.Select(filePath => Path.GetFullPath(filePath));

            AssertHelpers.SortAndEqual(expectFilesInFullpath, actualFilesInFullpath, StringComparer.InvariantCultureIgnoreCase);
        }
    }
}
