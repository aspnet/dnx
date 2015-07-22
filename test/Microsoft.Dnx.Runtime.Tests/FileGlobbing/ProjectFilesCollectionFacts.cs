// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.Json;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests.FileGlobbing
{
    public class ProjectFilesCollectionFacts
    {
        [Fact]
        public void DefaultPatternsAreSet()
        {
            var rawProject = Deserialize(@"
{
}");

            var target = new ProjectFilesCollection(rawProject, null, null);

            var sharedFilesPatternsGroup = target.SharedPatternsGroup;
            Assert.NotNull(sharedFilesPatternsGroup);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultSharedPatterns), sharedFilesPatternsGroup.IncludePatterns);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultBuiltInExcludePatterns), sharedFilesPatternsGroup.ExcludePatterns);
            Assert.Equal(0, sharedFilesPatternsGroup.IncludeLiterals.Count());
            Assert.Equal(0, sharedFilesPatternsGroup.ExcludePatternsGroup.Count());

            var resourceFilesPatternsGroup = target.ResourcePatternsGroup;
            Assert.NotNull(resourceFilesPatternsGroup);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultResourcesBuiltInPatterns), resourceFilesPatternsGroup.IncludePatterns);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultBuiltInExcludePatterns), resourceFilesPatternsGroup.ExcludePatterns);
            Assert.Equal(0, resourceFilesPatternsGroup.IncludeLiterals.Count());
            Assert.Equal(0, resourceFilesPatternsGroup.ExcludePatternsGroup.Count());

            var preprocessFilesPatternsGroup = target.PreprocessPatternsGroup;
            Assert.NotNull(preprocessFilesPatternsGroup);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultPreprocessPatterns), preprocessFilesPatternsGroup.IncludePatterns);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultBuiltInExcludePatterns), preprocessFilesPatternsGroup.ExcludePatterns);
            Assert.Equal(0, preprocessFilesPatternsGroup.IncludeLiterals.Count());
            Assert.Equal(2, preprocessFilesPatternsGroup.ExcludePatternsGroup.Count());

            var compileFilesPatternsGroup = target.CompilePatternsGroup;
            Assert.NotNull(compileFilesPatternsGroup);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultCompileBuiltInPatterns), compileFilesPatternsGroup.IncludePatterns);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultBuiltInExcludePatterns), compileFilesPatternsGroup.ExcludePatterns);
            Assert.Equal(0, compileFilesPatternsGroup.IncludeLiterals.Count());
            Assert.Equal(3, compileFilesPatternsGroup.ExcludePatternsGroup.Count());

            var contentFilePatternsGroup = target.ContentPatternsGroup;
            Assert.NotNull(contentFilePatternsGroup);
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultContentsBuiltInPatterns), contentFilePatternsGroup.IncludePatterns);
            Assert.Equal(
                NormalizePatterns(ProjectFilesCollection.DefaultBuiltInExcludePatterns.Concat(ProjectFilesCollection.DefaultPublishExcludePatterns).Distinct().OrderBy(elem => elem).ToArray()),
                contentFilePatternsGroup.ExcludePatterns.OrderBy(elem => elem));
            Assert.Equal(0, contentFilePatternsGroup.IncludeLiterals.Count());
            Assert.Equal(4, contentFilePatternsGroup.ExcludePatternsGroup.Count());
        }

        [Fact]
        public void FilesPatternsAreSet()
        {
            var rawProject = Deserialize(@"
         {
             ""compileBuiltIn"": """",
             ""compile"": ""*.cs;../*.cs"",
             ""compileExclude"": [""fake*.cs"", ""fake2*.cs""],
             ""compileFiles"": ""signle.cs"",
             ""shared"": ""shared/**/*.cs"",
             ""sharedExclude"": ""excludeShared*.cs"",
             ""sharedFiles"": ""shared.cs"",
             ""contentBuiltIn"": """",
             ""content"": ""**/*.content"",
             ""contentExclude"": ""excludecontent"",
             ""contentFiles"": [""additional.file"", ""another.file""],
             ""resourceBuiltIn"": ""resource/builtin/*.resx"",
             ""resource"": ""resx/**/*.content"",
             ""resourceExclude"": ""resx/exclude/**/*.content"",
             ""resourceFiles"": ""one-resource.file"",
             ""publishExclude"": ""no_pack/*.*"",
             ""exclude"": ""buggy/*.*""
         }");

            var target = new ProjectFilesCollection(rawProject, string.Empty, string.Empty);

            Assert.Equal(NormalizePatterns("*.cs", "../*.cs"), target.CompilePatternsGroup.IncludePatterns);
            Assert.Equal(NormalizePatterns("signle.cs"), target.CompilePatternsGroup.IncludeLiterals);
            Assert.Equal(NormalizePatterns("fake*.cs", "fake2*.cs", "buggy/*.*", "bin/**", "obj/**", "**/*.xproj"), target.CompilePatternsGroup.ExcludePatterns);

            Assert.Equal(NormalizePatterns("excludecontent", "buggy/*.*", "bin/**", "obj/**", "**/*.xproj", "no_pack/*.*"), target.ContentPatternsGroup.ExcludePatterns);
            Assert.Equal(NormalizePatterns("**/*.content"), target.ContentPatternsGroup.IncludePatterns);
            Assert.Equal(NormalizePatterns("additional.file", "another.file"), target.ContentPatternsGroup.IncludeLiterals);

            Assert.Equal(NormalizePatterns("resx/**/*.content", "resource/builtin/*.resx"), target.ResourcePatternsGroup.IncludePatterns);
            Assert.Equal(NormalizePatterns("resx/exclude/**/*.content", "buggy/*.*", "bin/**", "obj/**", "**/*.xproj"), target.ResourcePatternsGroup.ExcludePatterns);
            Assert.Equal(NormalizePatterns("one-resource.file"), target.ResourcePatternsGroup.IncludeLiterals);
        }

        [Fact]
        public void RewriteCompileBuiltIn()
        {
            var rawProject = Deserialize(@"
         {
             ""compileBuiltIn"": [""**/*.cpp"", ""**/*.h""],
             ""compile"": ""*.cs;../*.cs"",
             ""compileExclude"": [""fake*.cs"", ""fake2*.cs""]
         }");

            var target = new ProjectFilesCollection(rawProject, string.Empty, string.Empty);
            Assert.Equal(NormalizePatterns("*.cs", "../*.cs", "**/*.cpp", "**/*.h"), target.CompilePatternsGroup.IncludePatterns);
        }


        [Fact]
        public void ExceptionThrowWhenWildcardPresentsInLiteralPath()
        {
            var rawProject = Deserialize(@"{""compileFiles"": ""*.cs""}");

            var exception = Assert.Throws<FileFormatException>(() =>
            {
                var target = new ProjectFilesCollection(rawProject, string.Empty, string.Empty);
            });

            Assert.Equal(
                "The 'compileFiles' property cannot contain wildcard characters.",
                exception.InnerException.Message);
        }

        private JsonObject Deserialize(string content)
        {
            using (var reader = new StringReader(content))
            {
                return JsonDeserializer.Deserialize(reader) as JsonObject;
            }
        }

        private IEnumerable<string> NormalizePatterns(params string[] patterns)
        {
            return patterns.Select(pattern => pattern.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
