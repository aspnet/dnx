﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests.FileGlobbing
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
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultResourcesPatterns), resourceFilesPatternsGroup.IncludePatterns);
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
            Assert.Equal(NormalizePatterns(ProjectFilesCollection.DefaultContentsPatterns), contentFilePatternsGroup.IncludePatterns);
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
             ""sharedFiles"": ""**/*.cs"",
             ""publishExclude"": ""no_pack/*.*"",
             ""exclude"": ""buggy/*.*"",
         }");

            var target = new ProjectFilesCollection(rawProject, string.Empty, string.Empty);

            Assert.Equal(NormalizePatterns("*.cs", "../*.cs"), target.CompilePatternsGroup.IncludePatterns);
            Assert.Equal(NormalizePatterns("signle.cs"), target.CompilePatternsGroup.IncludeLiterals);
            Assert.Equal(NormalizePatterns("fake*.cs", "fake2*.cs", "buggy/*.*", "bin/**", "obj/**", "**/*.xproj"), target.CompilePatternsGroup.ExcludePatterns);
            Assert.Equal(NormalizePatterns("buggy/*.*", "bin/**", "obj/**", "**/*.xproj", "no_pack/*.*"), target.ContentPatternsGroup.ExcludePatterns);
        }

        [Fact]
        public void RewriteCompileBuiltIn()
        {
            var rawProject = Deserialize(@"
         {
             ""compileBuiltIn"": [""**/*.cpp"", ""**/*.h""],
             ""compile"": ""*.cs;../*.cs"",
             ""compileExclude"": [""fake*.cs"", ""fake2*.cs""],
         }");

            var target = new ProjectFilesCollection(rawProject, string.Empty, string.Empty);
            Assert.Equal(NormalizePatterns("*.cs", "../*.cs", "**/*.cpp", "**/*.h"), target.CompilePatternsGroup.IncludePatterns);
        }

        private JObject Deserialize(string content)
        {
            return JsonConvert.DeserializeObject<JObject>(content);
        }

        private IEnumerable<string> NormalizePatterns(params string[] patterns)
        {
            return patterns.Select(pattern => pattern.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}