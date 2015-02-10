// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime.FileGlobbing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class ProjectFilesCollectionFacts
    {
        [Fact]
        public void FilesPatternsAreSet()
        {
            var rawProject = Deserialize(@"
{
    ""code"": ""*.cs;../*.cs"",
    ""exclude"": ""buggy/*.*"",
    ""bundleExclude"": ""no_pack/*.*"",
    ""preprocess"": ""other/**/*.cs;*.cs;*.*"",
    ""shared"": ""shared/**/*.cs"",
    ""resources"": ""a.cs;foo.js""
}");

            var target = new ProjectFilesCollection(rawProject, string.Empty);


            Assert.Equal(new[] { "*.cs", @"../*.cs" }, target.SourcePatterns);
            Assert.Equal(new[] { @"buggy/*.*" }, target.ExcludePatterns);
            Assert.Equal(new[] { @"no_pack/*.*" }, target.BundleExcludePatterns);
            Assert.Equal(new[] { @"other/**/*.cs", "*.cs", "*.*" }, target.PreprocessPatterns);
            Assert.Equal(new[] { @"shared/**/*.cs" }, target.SharedPatterns);
            Assert.Equal(new[] { "a.cs", @"foo.js" }, target.ResourcesPatterns);
        }


        [Fact]
        public void FilePatternsWorkForArraysAreSet()
        {
            var rawProject = Deserialize(@"
{
    ""code"": [""*.cs"", ""../*.cs""],
    ""exclude"": [""buggy/*.*""],
    ""bundleExclude"": [""no_pack/*.*""],
    ""preprocess"": [""other/**/*.cs"", ""*.cs"", ""*.*""],
    ""shared"": [""shared/**/*.cs;../../shared/*.cs""],
    ""resources"": [""a.cs"", ""foo.js""]
}");

            var target = new ProjectFilesCollection(rawProject, string.Empty);

            Assert.Equal(new[] { "*.cs", @"../*.cs" }, target.SourcePatterns);
            Assert.Equal(new[] { @"buggy/*.*" }, target.ExcludePatterns);
            Assert.Equal(new[] { @"no_pack/*.*" }, target.BundleExcludePatterns);
            Assert.Equal(new[] { @"other/**/*.cs", "*.cs", "*.*" }, target.PreprocessPatterns);
            Assert.Equal(new[] { @"shared/**/*.cs", @"../../shared/*.cs" }, target.SharedPatterns);
            Assert.Equal(new[] { "a.cs", @"foo.js" }, target.ResourcesPatterns);
        }

        [Fact]
        public void DefaultSourcePatternsAreUsedIfNoneSpecified()
        {
            var rawProject = Deserialize(@"
{
}");

            var target = new ProjectFilesCollection(rawProject, string.Empty);

            Assert.Equal(ProjectFilesCollection.DefaultSourcePatterns, target.SourcePatterns);
            Assert.Equal(ProjectFilesCollection.DefaultExcludePatterns, target.ExcludePatterns);
            Assert.Equal(ProjectFilesCollection.DefaultBundleExcludePatterns, target.BundleExcludePatterns);
            Assert.Equal(ProjectFilesCollection.DefaultPreprocessPatterns, target.PreprocessPatterns);
            Assert.Equal(ProjectFilesCollection.DefaultSharedPatterns, target.SharedPatterns);
            Assert.Equal(ProjectFilesCollection.DefaultResourcesPatterns, target.ResourcesPatterns);
        }

        [Fact]
        public void NullSourcePatternReturnsEmptySet()
        {
            var rawProject = Deserialize(@"
{
    ""code"": null
}");

            var target = new ProjectFilesCollection(rawProject, string.Empty);

            Assert.Empty(target.SourcePatterns);
        }

        [Fact]
        public void EmptyStringAndNullElementsAreIgnored()
        {
            var rawProject = Deserialize(@"
{
    ""code"": [""a.cs"", """", ""b.cs;;;"", ""c.cs"", null],
    ""exclude"": ""a.cs;;;;""
}");

            var target = new ProjectFilesCollection(rawProject, string.Empty);

            Assert.Equal(new[] { "a.cs", "b.cs", "c.cs" }, target.SourcePatterns);
            Assert.Equal(new[] { "a.cs" }, target.ExcludePatterns);
        }

        [Theory]
        [InlineData("code", @"C:\test\**\*")]
        public void ThrowWhenContainsAbsolutePathInSource(string propertyName, string pattern)
        {
            var rawProject = Deserialize(@"
{
    ""<PROPERTY>"": ""<PATTERN>""
}
".Replace("<PROPERTY>", propertyName).Replace("<PATTERN>", pattern.Replace("\\", "\\\\")));

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                new ProjectFilesCollection(rawProject, string.Empty);
            });

            Assert.True(exception.Message.Contains(pattern));
        }

        private JObject Deserialize(string content)
        {
            return JsonConvert.DeserializeObject<JObject>(content);
        }
    }
}