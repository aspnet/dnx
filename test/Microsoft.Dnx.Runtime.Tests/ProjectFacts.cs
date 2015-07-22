// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Dnx.Runtime.Helpers;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
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
        public void VersionIsSet()
        {
            var project = Project.GetProject(@"{ ""version"": ""1.2.3"" }", @"foo", @"C:\Foo\Project.json");

            Assert.Equal(new SemanticVersion("1.2.3"), project.Version);
        }

        [Fact]
        public void AuthorsAreSet()
        {
            var project = Project.GetProject(@"{ ""authors"": [""Bob"", ""Dean""] }", @"foo", @"C:\Foo\Project.json");

            Assert.Equal("Bob", project.Authors[0]);
            Assert.Equal("Dean", project.Authors[1]);
        }

        [Fact]
        public void OwnersAreSet()
        {
            var project = Project.GetProject(@"{ ""owners"": [""Alice"", ""Chi""] }", @"foo", @"C:\Foo\Project.json");

            Assert.Equal("Alice", project.Owners[0]);
            Assert.Equal("Chi", project.Owners[1]);
        }

        [Theory]
        [InlineData("summary", nameof(Project.Summary))]
        [InlineData("description", nameof(Project.Description))]
        [InlineData("copyright", nameof(Project.Copyright))]
        [InlineData("title", nameof(Project.Title))]
        [InlineData("webroot", nameof(Project.WebRoot))]
        [InlineData("entryPoint", nameof(Project.EntryPoint))]
        [InlineData("projectUrl", nameof(Project.ProjectUrl))]
        [InlineData("licenseUrl", nameof(Project.LicenseUrl))]
        [InlineData("iconUrl", nameof(Project.IconUrl))]
        public void CommonStringPropertiesAreSet(string jsonPropertyName, string objectPropertyName)
        {
            var propertyInfo = typeof(Project).GetTypeInfo().GetDeclaredProperty(objectPropertyName);
            Assert.NotNull(propertyInfo);

            var expectValue = string.Format("this is a fake {0} at {1}", jsonPropertyName, DateTime.Now.Ticks);
            var projectContent = string.Format("{{ \"{0}\": \"{1}\" }}", jsonPropertyName, expectValue);
            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");

            var propertyValue = (string)propertyInfo.GetValue(project);
            Assert.Equal(expectValue, propertyValue);
        }

        [Theory]
        [InlineData("requireLicenseAcceptance", null, nameof(Project.RequireLicenseAcceptance), false)]
        [InlineData("requireLicenseAcceptance", "false", nameof(Project.RequireLicenseAcceptance), false)]
        [InlineData("requireLicenseAcceptance", "true", nameof(Project.RequireLicenseAcceptance), true)]
        [InlineData("loadable", null, nameof(Project.IsLoadable), true)]
        [InlineData("loadable", "false", nameof(Project.IsLoadable), false)]
        [InlineData("loadable", "true", nameof(Project.IsLoadable), true)]
        [InlineData("embedInteropTypes", null, nameof(Project.EmbedInteropTypes), false)]
        [InlineData("embedInteropTypes", "false", nameof(Project.EmbedInteropTypes), false)]
        [InlineData("embedInteropTypes", "true", nameof(Project.EmbedInteropTypes), true)]
        public void CommonBooleanPropertiesAreSet(string jsonPropertyName,
                                                  string jsonPropertyValue,
                                                  string objectPropertyName,
                                                  bool expectResult)
        {
            var propertyInfo = typeof(Project).GetTypeInfo().GetDeclaredProperty(objectPropertyName);
            Assert.NotNull(propertyInfo);

            string projectContent;
            if (jsonPropertyValue != null)
            {
                projectContent = string.Format("{{ \"{0}\": {1} }}", jsonPropertyName, jsonPropertyValue);
            }
            else
            {
                projectContent = @"{}";
            }

            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");

            var propertyValue = (bool)propertyInfo.GetValue(project);
            Assert.Equal(expectResult, propertyValue);
        }

        [Fact]
        public void CompilerServicesIsNullByDefault()
        {
            var projectContent = @"{}";
            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");
            Assert.Null(project.CompilerServices);
        }

        [Fact]
        public void CompilerServicesDefaultValues()
        {
            var projectContent = @"{""compiler"": {} }";
            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");

            Assert.NotNull(project.CompilerServices);
            Assert.Equal("C#", project.CompilerServices.Name);
            Assert.Null(project.CompilerServices.ProjectCompiler.AssemblyName);
            Assert.Null(project.CompilerServices.ProjectCompiler.TypeName);
        }

        [Fact]
        public void CompilerServicesValuesAreSet()
        {
            var projectContent = @"
{
    ""compiler"": {
        ""name"": ""Zulu#"",
        ""compilerAssembly"": ""Zulu.Compiler.dll"",
        ""compilerType"": ""Zulu.Compilation.Compiler""
    }
}";
            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");

            Assert.NotNull(project.CompilerServices);
            Assert.Equal("Zulu#", project.CompilerServices.Name);
            Assert.Equal("Zulu.Compiler.dll", project.CompilerServices.ProjectCompiler.AssemblyName);
            Assert.Equal("Zulu.Compilation.Compiler", project.CompilerServices.ProjectCompiler.TypeName);
        }

        [Fact]
        public void CommandsSetIsEmptyByDefault()
        {
            var projectContent = @"{}";
            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");
            Assert.NotNull(project.Commands);
            Assert.Equal(0, project.Commands.Count);
        }

        [Fact]
        public void CommandsSetIsSet()
        {
            var projectContent = @"
{
    ""commands"": {
        ""cmd1"": ""cmd1value"",
        ""cmd2"": ""cmd2value"",
        ""cmd3"": ""cmd3value""
    }
}";
            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");
            Assert.NotNull(project.Commands);
            Assert.Equal(3, project.Commands.Count);
        }

        [Fact]
        public void ScriptsSetIsEmptyByDefault()
        {
            var projectContent = @"{}";
            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");
            Assert.NotNull(project.Scripts);
            Assert.Equal(0, project.Scripts.Count);
        }

        [Fact]
        public void ScriptsSetIsSet()
        {
            var projectContent = @"
{
    ""scripts"": {
        ""GroupA"": ""GroupA first command"",
        ""GroupB"": [
            ""GroupeB first command"",
            ""GroupeB second command"",
            ""GroupeB third command""
        ]
    }
}";
            var project = Project.GetProject(projectContent, @"foo", @"C:\Foo\Project.json");

            Assert.NotNull(project.Scripts);
            Assert.Equal(2, project.Scripts.Count);

            var scriptGroupA = project.Scripts["GroupA"];
            Assert.Equal(1, scriptGroupA.Count());
            Assert.Equal("GroupA first command", scriptGroupA.First());

            var scriptGroupB = project.Scripts["GroupB"];
            Assert.Equal(3, scriptGroupB.Count());
            Assert.Equal("GroupeB first command", scriptGroupB.ElementAt(0));
            Assert.Equal("GroupeB second command", scriptGroupB.ElementAt(1));
            Assert.Equal("GroupeB third command", scriptGroupB.ElementAt(2));
        }

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
        ""E"": ""[1.0.0, 1.1.0)"",
        ""F"": { ""type"": ""build"" },
    }
}",
"foo",
@"c:\foo\project.json");

            Assert.NotNull(project.Dependencies);
            Assert.Equal(6, project.Dependencies.Count);
            var d1 = project.Dependencies[0];
            var d2 = project.Dependencies[1];
            var d3 = project.Dependencies[2];
            var d4 = project.Dependencies[3];
            var d5 = project.Dependencies[4];
            var d6 = project.Dependencies[5];
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
            Assert.Equal("F", d6.Name);
            Assert.Null(d6.LibraryRange.VersionRange);
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
            Assert.Equal("fx/A", d1.Name);
            Assert.Null(d1.LibraryRange.VersionRange);
            Assert.True(d1.LibraryRange.IsGacOrFrameworkReference);
            Assert.Equal("fx/B", d2.Name);
            Assert.Equal(SemanticVersion.Parse("1.0-alpha"), d2.LibraryRange.VersionRange.MinVersion);
            Assert.True(d2.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.True(d2.LibraryRange.IsGacOrFrameworkReference);
            Assert.Equal("fx/C", d3.Name);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), d3.LibraryRange.VersionRange.MinVersion);
            Assert.False(d3.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.True(d3.LibraryRange.IsGacOrFrameworkReference);
            Assert.Equal("fx/D", d4.Name);
            Assert.Equal(SemanticVersion.Parse("2.0.0"), d4.LibraryRange.VersionRange.MinVersion);
            Assert.False(d4.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.Prerelease);
            Assert.True(d4.LibraryRange.IsGacOrFrameworkReference);
        }

        [Fact]
        public void CompilerOptionsAreSet()
        {
            var project = Project.GetProject(@"
{
    ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""X"", ""y""], ""platform"": ""x86"", ""warningsAsErrors"": true, ""optimize"": true, ""keyFile"" : ""c:\\keyfile.snk"", ""delaySign"" : true, ""strongName"" : true }
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
            Assert.Equal(compilerOptions.KeyFile, @"c:\keyfile.snk");
            Assert.True(compilerOptions.DelaySign);
            Assert.True(compilerOptions.StrongName);
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
        ""dnx451"": {
        },
        ""dnxcore50"": {
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
            var net45Options = project.GetCompilerOptions(FrameworkNameHelper.ParseFrameworkName("net45"));
            Assert.NotNull(net45Options);
            Assert.True(net45Options.AllowUnsafe.Value);
            Assert.Equal(new[] { "X", "y", "NET45" }, net45Options.Defines);
            Assert.True(net45Options.WarningsAsErrors.Value);
            Assert.Equal("x86", net45Options.Platform);

            var dnx451Options = project.GetCompilerOptions(FrameworkNameHelper.ParseFrameworkName("dnx451"));
            Assert.NotNull(dnx451Options);
            Assert.Equal(new[] { "DNX451" }, dnx451Options.Defines);
            Assert.Null(dnx451Options.AllowUnsafe);

            var aspnetCore50Options = project.GetCompilerOptions(FrameworkNameHelper.ParseFrameworkName("dnxcore50"));
            Assert.NotNull(aspnetCore50Options);
            Assert.Equal(new[] { "X", "DNXCORE50" }, aspnetCore50Options.Defines);
            Assert.True(aspnetCore50Options.WarningsAsErrors.Value);

            var k10Options = project.GetCompilerOptions(FrameworkNameHelper.ParseFrameworkName("k10"));
            Assert.NotNull(k10Options);
            Assert.Null(k10Options.AllowUnsafe);
            Assert.Equal(new[] { "K10" }, k10Options.Defines);
            Assert.True(k10Options.WarningsAsErrors.Value);
            Assert.Null(k10Options.Platform);
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
