// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Xunit;
using NuGet;

namespace Microsoft.Framework.Runtime.Tests
{
    public class Program
    {
        public void Main(string[] args)
        {
            new ProjectFacts().DependenciesAreSet();
        }
    }

    public class ProjectFacts
    {
        [Fact]
        public void NameIsIgnoredIsSpecified()
        {
            // Arrange & Act
            var project = Project.GetProject(@"{ ""name"": ""hello"" }", @"foo", @"c:\foo\project.json");

            // Assert
            Assert.Equal("foo", project.Name);
        }

        [Fact]
        public void GetProjectNormalizesPaths()
        {
            var project = Project.GetProject(@"{}", "name", "../../foo");

            Assert.True(Path.IsPathRooted(project.ProjectFilePath));
        }

        [Fact]
        public void CommandsAreSet()
        {
            var project = Project.GetProject(@"
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
            var project = Project.GetProject(@"
{
    ""dependencies"": {  
        ""A"": """",
        ""B"": ""1.0-alpha-*"",
        ""C"": ""1.0.0"",
        ""D"": { ""version"": ""2.0.0"" },
        ""E"": ""[1.0.0, 1.1.0)""
    }
}",
"foo",
@"c:\foo\project.json");

            Assert.NotNull(project.Dependencies);
            Assert.Equal(5, project.Dependencies.Count);
            var d1 = project.Dependencies[0];
            var d2 = project.Dependencies[1];
            var d3 = project.Dependencies[2];
            var d4 = project.Dependencies[3];
            var d5 = project.Dependencies[4];
            Assert.Equal("A", d1.Name);
            Assert.Null(d1.LibraryRange.VersionRange);
            Assert.Equal("B", d2.Name);
            Assert.Equal(SemanticVersion.Parse("1.0-alpha"), d2.LibraryRange.VersionRange.MinVersion);
            Assert.True(d2.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.Equal("C", d3.Name);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), d3.LibraryRange.VersionRange.MinVersion);
            Assert.False(d3.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.Equal("D", d4.Name);
            Assert.Equal(SemanticVersion.Parse("2.0.0"), d4.LibraryRange.VersionRange.MinVersion);
            Assert.False(d4.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.Equal("E", d5.Name);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), d5.LibraryRange.VersionRange.MinVersion);
            Assert.Equal(SemanticVersion.Parse("1.1.0"), d5.LibraryRange.VersionRange.MaxVersion);
            Assert.False(d5.LibraryRange.VersionRange.IsMaxInclusive);
        }

        [Fact]
        public void DependenciesAreSetPerTargetFramework()
        {
            var project = Project.GetProject(@"
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
            Assert.Null(d1.LibraryRange.VersionRange);
            Assert.Equal("B", d2.Name);
            Assert.Equal(SemanticVersion.Parse("1.0-alpha"), d2.LibraryRange.VersionRange.MinVersion);
            Assert.True(d2.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.Equal("C", d3.Name);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), d3.LibraryRange.VersionRange.MinVersion);
            Assert.False(d3.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.Equal("D", d4.Name);
            Assert.Equal(SemanticVersion.Parse("2.0.0"), d4.LibraryRange.VersionRange.MinVersion);
            Assert.False(d4.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
        }

        [Fact]
        public void FrameworkAssembliesAreSet()
        {
            var project = Project.GetProject(@"
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
            Assert.Null(d1.LibraryRange.VersionRange);
            Assert.True(d1.LibraryRange.IsGacOrFrameworkReference);
            Assert.Equal("B", d2.Name);
            Assert.Equal(SemanticVersion.Parse("1.0-alpha"), d2.LibraryRange.VersionRange.MinVersion);
            Assert.True(d2.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.True(d2.LibraryRange.IsGacOrFrameworkReference);
            Assert.Equal("C", d3.Name);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), d3.LibraryRange.VersionRange.MinVersion);
            Assert.False(d3.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.True(d3.LibraryRange.IsGacOrFrameworkReference);
            Assert.Equal("D", d4.Name);
            Assert.Equal(SemanticVersion.Parse("2.0.0"), d4.LibraryRange.VersionRange.MinVersion);
            Assert.False(d4.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.True(d4.LibraryRange.IsGacOrFrameworkReference);
        }

        [Fact]
        public void CompilerOptionsAreSet()
        {
            var project = Project.GetProject(@"
{
    ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""X"", ""y""], ""platform"": ""x86"", ""warningsAsErrors"": true, ""optimize"": true }
}",
"foo",
@"c:\foo\project.json");

            var compilerOptions = project.GetCompilerOptions();
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
            var project = Project.GetProject(@"
{}",
"foo",
@"c:\foo\project.json");

            var compilerOptions = project.GetCompilerOptions();
            Assert.NotNull(compilerOptions);
            Assert.Null(compilerOptions.Defines);
        }

        [Fact]
        public void CompilerOptionsAreSetPerConfiguration()
        {
            var project = Project.GetProject(@"
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

            var compilerOptions = project.GetCompilerOptions();
            Assert.NotNull(compilerOptions);
            var net45Options = project.GetCompilerOptions(Project.ParseFrameworkName("net45"));
            Assert.NotNull(net45Options);
            Assert.True(net45Options.AllowUnsafe.Value);
            Assert.Equal(new[] { "X", "y", "NET45" }, net45Options.Defines);
            Assert.True(net45Options.WarningsAsErrors.Value);
            Assert.Equal("x86", net45Options.Platform);

            var aspnet50Options = project.GetCompilerOptions(Project.ParseFrameworkName("aspnet50"));
            Assert.NotNull(aspnet50Options);
            Assert.Equal(new[] { "ASPNET50" }, aspnet50Options.Defines);

            var aspnetCore50Options = project.GetCompilerOptions(Project.ParseFrameworkName("aspnetcore50"));
            Assert.NotNull(aspnetCore50Options);
            Assert.Equal(new[] { "X", "ASPNETCORE50" }, aspnetCore50Options.Defines);
            Assert.True(aspnetCore50Options.WarningsAsErrors.Value);

            var k10Options = project.GetCompilerOptions(Project.ParseFrameworkName("k10"));
            Assert.NotNull(k10Options);
            Assert.Null(k10Options.AllowUnsafe);
            Assert.Equal(new[] { "K10" }, k10Options.Defines);
            Assert.True(k10Options.WarningsAsErrors.Value);
            Assert.Null(k10Options.Platform);
        }

        [Fact]
        public void SourcePatternsAreSet()
        {
            var project = Project.GetProject(@"
{
    ""code"": ""*.cs;../*.cs"",
    ""exclude"": ""buggy/*.*"",
    ""bundleExclude"": ""no_pack/*.*"",
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
            var project = Project.GetProject(@"
{
    ""code"": [""*.cs"", ""../*.cs""],
    ""exclude"": [""buggy/*.*""],
    ""bundleExclude"": [""no_pack/*.*""],
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
            var project = Project.GetProject(@"
{
}",
"foo",
@"c:\foo\project.json");

            Assert.Equal(Project._defaultSourcePatterns, project.SourcePatterns);
            Assert.Equal(Project._defaultExcludePatterns, project.ExcludePatterns);
            Assert.Equal(Project._defaultPackExcludePatterns, project.PackExcludePatterns);
            Assert.Equal(Project._defaultPreprocessPatterns, project.PreprocessPatterns);
            Assert.Equal(Project._defaultSharedPatterns, project.SharedPatterns);
            Assert.Equal(Project._defaultResourcesPatterns, project.ResourcesPatterns);
        }

        [Fact]
        public void NullSourcePatternReturnsEmptySet()
        {
            var project = Project.GetProject(@"
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
            var project = Project.GetProject(@"
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
            var project = Project.GetProject(@"
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
            var project = Project.GetProject(@"
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
            var project = Project.GetProject(@" { }", "foo", @"c:\foo\project.json");

            Assert.False(project.RequireLicenseAcceptance);
        }

        [Fact]
        public void TagsAreSet()
        {
            var project = Project.GetProject(@"
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
            var project = Project.GetProject(@" { }", "foo", @"c:\foo\project.json");

            Assert.NotNull(project.Tags);
            Assert.Equal(0, project.Tags.Count());
        }
    }
}
