// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Loader;
using NuGet;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class ProjectFacts
    {
        [Fact]
        public void NameIsIgnoredIsSpecified()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"{ ""name"": ""hello"" }", @"foo", @"c:\foo\project.json");

            // Assert
            Assert.Equal("foo", project.Name);
        }

        [Fact]
        public void GetProjectNormalizesPaths()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"{}", "name", "../../foo");

            Assert.True(Path.IsPathRooted(project.ProjectFilePath));
        }

        [Fact]
        public void CommandsAreSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""commands"": { ""web"": ""Microsoft.AspNet.Hosting something"" }
}",
"foo",
@"c:\foo\project.json");

            Assert.Equal(1, project.Commands.Count);
            Assert.True(project.Commands.ContainsKey("web"));
            Assert.Equal("Microsoft.AspNet.Hosting something", project.Commands["web"]);
            Assert.True(project.Commands.ContainsKey("Web"));
        }

        [Fact]
        public void DependenciesAreSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""dependencies"": {  
        ""A"": """",
        ""B"": ""1.0-alpha-*"",
        ""C"": ""1.0.0"",
        ""D"": { ""version"": ""2.0.0"" }
    }
}",
"foo",
@"c:\foo\project.json");

            Assert.NotNull(project.Dependencies);
            Assert.Equal(4, project.Dependencies.Count);
            var d1 = project.Dependencies[0];
            var d2 = project.Dependencies[1];
            var d3 = project.Dependencies[2];
            var d4 = project.Dependencies[3];
            Assert.Equal("A", d1.Name);
            Assert.Null(d1.Version);
            Assert.Equal("B", d2.Name);
            Assert.Equal(SemanticVersion.Parse("1.0-alpha-*"), d2.Version);
            Assert.True(d2.Version.IsSnapshot);
            Assert.Equal("C", d3.Name);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), d3.Version);
            Assert.False(d3.Version.IsSnapshot);
            Assert.Equal("D", d4.Name);
            Assert.Equal(SemanticVersion.Parse("2.0.0"), d4.Version);
            Assert.False(d4.Version.IsSnapshot);
        }

        [Fact]
        public void DependenciesAreSetPerTargetFramework()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""frameworks"": {
        ""net45"": {
            ""dependencies"": {  
                ""A"": """",
                ""B"": ""1.0-alpha-*"",
                ""C"": ""1.0.0"",
                ""D"": { ""version"": ""2.0.0"" }
            }
        }
    }
}",
"foo",
@"c:\foo\project.json");

            Assert.Empty(project.Dependencies);
            var targetFrameworkInfo = project.GetTargetFrameworks().First();
            Assert.NotNull(targetFrameworkInfo.Dependencies);
            Assert.Equal(4, targetFrameworkInfo.Dependencies.Count);
            var d1 = targetFrameworkInfo.Dependencies[0];
            var d2 = targetFrameworkInfo.Dependencies[1];
            var d3 = targetFrameworkInfo.Dependencies[2];
            var d4 = targetFrameworkInfo.Dependencies[3];
            Assert.Equal("A", d1.Name);
            Assert.Null(d1.Version);
            Assert.Equal("B", d2.Name);
            Assert.Equal(SemanticVersion.Parse("1.0-alpha-*"), d2.Version);
            Assert.True(d2.Version.IsSnapshot);
            Assert.Equal("C", d3.Name);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), d3.Version);
            Assert.False(d3.Version.IsSnapshot);
            Assert.Equal("D", d4.Name);
            Assert.Equal(SemanticVersion.Parse("2.0.0"), d4.Version);
            Assert.False(d4.Version.IsSnapshot);
        }

        [Fact]
        public void FrameworkAssembliesAreSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""frameworks"": {
        ""net45"": {
            ""frameworkAssemblies"": {  
                ""A"": """",
                ""B"": ""1.0-alpha-*"",
                ""C"": ""1.0.0"",
                ""D"": { ""version"": ""2.0.0"" }
            }
        }
    }
}",
"foo",
@"c:\foo\project.json");

            Assert.Empty(project.Dependencies);
            var targetFrameworkInfo = project.GetTargetFrameworks().First();
            Assert.Equal(4, targetFrameworkInfo.Dependencies.Count);
            var d1 = targetFrameworkInfo.Dependencies[0];
            var d2 = targetFrameworkInfo.Dependencies[1];
            var d3 = targetFrameworkInfo.Dependencies[2];
            var d4 = targetFrameworkInfo.Dependencies[3];
            Assert.Equal("A", d1.Name);
            Assert.Null(d1.Version);
            Assert.True(d1.IsGacOrFrameworkReference);
            Assert.Equal("B", d2.Name);
            Assert.Equal(SemanticVersion.Parse("1.0-alpha-*"), d2.Version);
            Assert.True(d2.Version.IsSnapshot);
            Assert.True(d2.IsGacOrFrameworkReference);
            Assert.Equal("C", d3.Name);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), d3.Version);
            Assert.False(d3.Version.IsSnapshot);
            Assert.True(d3.IsGacOrFrameworkReference);
            Assert.Equal("D", d4.Name);
            Assert.Equal(SemanticVersion.Parse("2.0.0"), d4.Version);
            Assert.False(d4.Version.IsSnapshot);
            Assert.True(d4.IsGacOrFrameworkReference);
        }

        [Fact]
        public void CompilerOptionsAreSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""X"", ""y""], ""platform"": ""x86"", ""warningsAsErrors"": true, ""optimize"": true }
}",
"foo",
@"c:\foo\project.json");

            var compilerOptions = Assert.IsType<RoslynCompilerOptions>(project.GetCompilerOptions());
            Assert.NotNull(compilerOptions);
            Assert.True(compilerOptions.AllowUnsafe.Value);
            Assert.Equal(new[] { "X", "y" }, compilerOptions.Defines);
            Assert.True(compilerOptions.WarningsAsErrors.Value);
            Assert.Equal("x86", compilerOptions.Platform);
            Assert.True(compilerOptions.Optimize.Value);
        }

        [Fact]
        public void CompilerOptionsAreNotNullIfNotSpecified()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{}",
"foo",
@"c:\foo\project.json");

            var compilerOptions = Assert.IsType<RoslynCompilerOptions>(project.GetCompilerOptions());
            Assert.NotNull(compilerOptions);
            Assert.Null(compilerOptions.Defines);
        }

        [Fact]
        public void CompilerOptionsAreSetPerConfiguration()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""frameworks"" : {
        ""net45"":  {
            ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""X"", ""y""], ""platform"": ""x86"", ""warningsAsErrors"": true }
        },
        ""aspnet50"": {
            
        },
        ""aspnetcore50"": {
            ""compilationOptions"": { ""define"": [""X""], ""warningsAsErrors"": true }
        },
        ""k10"": {
            ""compilationOptions"": { ""warningsAsErrors"": true }
        }
    }
}",
"foo",
@"c:\foo\project.json");

            var compilerOptions = Assert.IsType<RoslynCompilerOptions>(project.GetCompilerOptions());
            Assert.NotNull(compilerOptions);
            var net45Options = Assert.IsType<RoslynCompilerOptions>(
                project.GetCompilerOptions(ProjectReader.ParseFrameworkName("net45")));
            Assert.NotNull(net45Options);
            Assert.True(net45Options.AllowUnsafe.Value);
            Assert.Equal(new[] { "X", "y", "NET45" }, net45Options.Defines);
            Assert.True(net45Options.WarningsAsErrors.Value);
            Assert.Equal("x86", net45Options.Platform);

            var aspnet50Options = Assert.IsType<RoslynCompilerOptions>(
                project.GetCompilerOptions(ProjectReader.ParseFrameworkName("aspnet50")));
            Assert.NotNull(aspnet50Options);
            Assert.Equal(new[] { "ASPNET50" }, aspnet50Options.Defines);

            var aspnetCore50Options = Assert.IsType<RoslynCompilerOptions>(
                project.GetCompilerOptions(ProjectReader.ParseFrameworkName("aspnetcore50")));
            Assert.NotNull(aspnetCore50Options);
            Assert.Equal(new[] { "X", "ASPNETCORE50" }, aspnetCore50Options.Defines);
            Assert.True(aspnetCore50Options.WarningsAsErrors.Value);

            var k10Options = Assert.IsType<RoslynCompilerOptions>(
                project.GetCompilerOptions(ProjectReader.ParseFrameworkName("k10")));
            Assert.NotNull(k10Options);
            Assert.Null(k10Options.AllowUnsafe);
            Assert.Equal(new[] { "K10" }, k10Options.Defines);
            Assert.True(k10Options.WarningsAsErrors.Value);
            Assert.Null(k10Options.Platform);
        }

        [Fact]
        public void SourcePatternsAreSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""code"": ""*.cs;../*.cs"",
    ""exclude"": ""buggy/*.*"",
    ""packExclude"": ""no_pack/*.*"",
    ""preprocess"": ""other/**/*.cs;*.cs;*.*"",
    ""shared"": ""shared/**/*.cs"",
    ""resources"": ""a.cs;foo.js""
}",
"foo",
@"c:\foo\project.json");

            Assert.Equal(new[] { "*.cs", @"../*.cs" }, project.SourcePatterns);
            Assert.Equal(new[] { @"buggy/*.*" }, project.ExcludePatterns);
            Assert.Equal(new[] { @"no_pack/*.*" }, project.PackExcludePatterns);
            Assert.Equal(new[] { @"other/**/*.cs", "*.cs", "*.*" }, project.PreprocessPatterns);
            Assert.Equal(new[] { @"shared/**/*.cs" }, project.SharedPatterns);
            Assert.Equal(new[] { "a.cs", @"foo.js" }, project.ResourcesPatterns);
        }

        [Fact]
        public void SourcePatternsWorkForArraysAreSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""code"": [""*.cs"", ""../*.cs""],
    ""exclude"": [""buggy/*.*""],
    ""packExclude"": [""no_pack/*.*""],
    ""preprocess"": [""other/**/*.cs"", ""*.cs"", ""*.*""],
    ""shared"": [""shared/**/*.cs;../../shared/*.cs""],
    ""resources"": [""a.cs"", ""foo.js""]
}",
"foo",
@"c:\foo\project.json");

            Assert.Equal(new[] { "*.cs", @"../*.cs" }, project.SourcePatterns);
            Assert.Equal(new[] { @"buggy/*.*" }, project.ExcludePatterns);
            Assert.Equal(new[] { @"no_pack/*.*" }, project.PackExcludePatterns);
            Assert.Equal(new[] { @"other/**/*.cs", "*.cs", "*.*" }, project.PreprocessPatterns);
            Assert.Equal(new[] { @"shared/**/*.cs", @"../../shared/*.cs" }, project.SharedPatterns);
            Assert.Equal(new[] { "a.cs", @"foo.js" }, project.ResourcesPatterns);
        }

        [Fact]
        public void DefaultSourcePatternsAreUsedIfNoneSpecified()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
}",
"foo",
@"c:\foo\project.json");

            Assert.Equal(ProjectReader._defaultSourcePatterns, project.SourcePatterns);
            Assert.Equal(ProjectReader._defaultExcludePatterns, project.ExcludePatterns);
            Assert.Equal(ProjectReader._defaultPackExcludePatterns, project.PackExcludePatterns);
            Assert.Equal(ProjectReader._defaultPreprocessPatterns, project.PreprocessPatterns);
            Assert.Equal(ProjectReader._defaultSharedPatterns, project.SharedPatterns);
            Assert.Equal(ProjectReader._defaultResourcesPatterns, project.ResourcesPatterns);
        }

        [Fact]
        public void NullSourcePatternReturnsEmptySet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""code"": null
}",
"foo",
@"c:\foo\project.json");

            Assert.Empty(project.SourcePatterns);
        }

        [Fact]
        public void EmptyStringAndNullElementsAreIgnored()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""code"": [""a.cs"", """", ""b.cs;;;"", ""c.cs"", null],
    ""exclude"": ""a.cs;;;;""
}",
"foo",
@"c:\foo\project.json");

            Assert.Equal(new[] { "a.cs", "b.cs", "c.cs" }, project.SourcePatterns);
            Assert.Equal(new[] { "a.cs" }, project.ExcludePatterns);
        }

        [Fact]
        public void ProjectUrlIsSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""projectUrl"": ""https://github.com/aspnet/KRuntime""
}",
"foo",
@"c:\foo\project.json");

            Assert.Equal("https://github.com/aspnet/KRuntime", project.ProjectUrl);
        }

        [Fact]
        public void RequireLicenseAcceptanceIsSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""requireLicenseAcceptance"": ""true""
}",
"foo",
@"c:\foo\project.json");

            Assert.True(project.RequireLicenseAcceptance);
        }

        [Fact]
        public void RequireLicenseAcceptanceDefaultValueIsFalse()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@" { }", "foo", @"c:\foo\project.json");

            Assert.False(project.RequireLicenseAcceptance);
        }

        [Fact]
        public void TagsAreSet()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@"
{
    ""tags"": [""awesome"", ""fantastic"", ""aspnet""]
}",
"foo",
@"c:\foo\project.json");
            var tags = new ReadOnlyHashSet<string>(project.Tags);

            Assert.Equal(3, tags.Count);
            Assert.True(tags.Contains("awesome"));
            Assert.True(tags.Contains("fantastic"));
            Assert.True(tags.Contains("aspnet"));
        }

        [Fact]
        public void EmptyTagsListWhenNotSpecified()
        {
            // Arrange
            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(@" { }", "foo", @"c:\foo\project.json");

            Assert.NotNull(project.Tags);
            Assert.Equal(0, project.Tags.Count());
        }

        [Fact]
        public void GetProject_UsesSpecifiedCompilerOptionsReader()
        {
            // Arrange
            var content =
@"{
    compilationOptions: { globalkey: 'globalvalue' },
    language: {
       assembly: '" + typeof(TestCompilerOptionsReader).GetTypeInfo().Assembly.FullName + @"',
       compilerOptionsReaderType: '" + typeof(TestCompilerOptionsReader).GetTypeInfo().FullName + @"'
    },
    frameworks: {
        aspnet50: {
            compilationOptions: { aspnet50Key: 'aspnet50value' }
       }
    },
    configurations: {
       release: {
            compilationOptions: { release_key: 'releasevalue' }
       }
    }
}";

            var reader = GetProjectReader();

            // Act
            var project = reader.GetProject(content, "testproject", "testpath");

            // Assert
            var options = Assert.IsType<TestCompilerOptions>(project.GetCompilerOptions());
            Assert.Equal(
@"{
  ""globalkey"": ""globalvalue""
}", options.Json);

            options = Assert.IsType<TestCompilerOptions>(project.GetCompilerOptions(ProjectReader.ParseFrameworkName("aspnet50")));
            Assert.Equal(

@"{
  ""aspnet50Key"": ""aspnet50value""
}", options.Json);

            options = Assert.IsType<TestCompilerOptions>(project.GetCompilerOptions("release"));
            Assert.Equal(

@"{
  ""release_key"": ""releasevalue""
}", options.Json);
        }

        private static ProjectReader GetProjectReader()
        {
            return new ProjectReader(LoadContextAccessor.Instance.Default);
        }

        private sealed class TestCompilerOptionsReader : ICompilerOptionsReader
        {
            public ICompilerOptions ReadCompilerOptions(string json)
            {
                return new TestCompilerOptions(json);
            }

            public ICompilerOptions ReadConfigurationCompilerOptions(string json, string configuration)
            {
                return new TestCompilerOptions(json);
            }

            public ICompilerOptions ReadFrameworkCompilerOptions(string json, string shortName, FrameworkName targetFramework)
            {
                return new TestCompilerOptions(json);
            }
        }

        private class TestCompilerOptions : ICompilerOptions
        {
            public TestCompilerOptions(string json)
            {
                Json = json;
            }

            public string Json { get; }

            public ICompilerOptions Merge(ICompilerOptions options)
            {
                return new TestCompilerOptions(Json + "|" + ((TestCompilerOptions)options).Json);
            }
        }
    }
}
