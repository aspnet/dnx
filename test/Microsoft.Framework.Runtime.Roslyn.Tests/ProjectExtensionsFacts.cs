// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.Framework.Runtime.Roslyn.Tests
{
    public class ProjectExtensionsFacts
    {
        [Fact]
        public void DefaultDesktopCompilationSettings()
        {
            var project = Project.GetProject(
@"{

}",
"foo",
@"c\foo\project.json");

            var settings = project.GetCompilationSettings("net45");

            Assert.NotNull(settings);
            Assert.NotNull(settings.Defines);
            Assert.Equal(new[] { "DEBUG", "TRACE" }, settings.Defines);
            Assert.Equal(LanguageVersion.CSharp6, settings.LanguageVersion);
            Assert.IsType<DesktopAssemblyIdentityComparer>(settings.CompilationOptions.AssemblyIdentityComparer);
            Assert.Equal(OutputKind.DynamicallyLinkedLibrary, settings.CompilationOptions.OutputKind);
        }

        [Fact]
        public void ChangingLanguageVersionIsEffective()
        {
            var project = Project.GetProject(
@"{
    ""compilationOptions"": { ""languageVersion"" : ""CSharp3"" }
}",
"foo",
@"c\foo\project.json");

            var settings = project.GetCompilationSettings("net45");

            Assert.Equal(LanguageVersion.CSharp3, settings.LanguageVersion);
        }

        [Theory]
        [InlineData("net45", "DEBUG,TRACE")]
        [InlineData("k10", "DEBUG,TRACE")]
        public void DefaultDefines(string shortName, string define)
        {
            var project = Project.GetProject(
@"{

}",
"foo",
@"c\foo\project.json");

            var settings = project.GetCompilationSettings(shortName);

            Assert.NotNull(settings);
            Assert.NotNull(settings.Defines);
            Assert.Equal(define.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                         settings.Defines);
        }

        [Fact]
        public void DefinesFromTopLevelAreCombinedWithFrameworkSpecific()
        {
            var project = Project.GetProject(
@"{
    ""compilationOptions"": { ""define"": [""X""] },
    ""frameworks"": {
        ""net45"": { 
            ""compilationOptions"": { ""define"": [""NET45"", ""Something""] }        
        }
    }
}",
"foo",
@"c\foo\project.json");
            var settings = project.GetCompilationSettings("net45");

            Assert.NotNull(settings);
            Assert.NotNull(settings.Defines);
            Assert.Equal(new[] { "X", "DEBUG", "TRACE", "NET45", "Something" }, settings.Defines);
        }

        [Fact]
        public void CompilerOptionsAreSetPerConfiguration()
        {
            var project = Project.GetProject(@"
{
    ""frameworks"" : {
        ""net45"":  {
            ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""X"", ""y""], ""platform"": ""x86"", ""warningsAsErrors"": true, ""optimize"": true, ""debugSymbols"": ""none"" }
        },
        ""k10"": {
            ""compilationOptions"": { ""warningsAsErrors"": true, ""debugSymbols"": ""pdbOnly"" }
        }
    }
}",
"foo",
@"c:\foo\project.json");

            var net45Options = project.GetCompilationSettings("net45");
            var k10Options = project.GetCompilationSettings("k10");

            Assert.True(net45Options.CompilationOptions.AllowUnsafe);
            Assert.Equal(new[] { "DEBUG", "TRACE", "X", "y", "NET45" }, net45Options.Defines);
            Assert.Equal(Platform.X86, net45Options.CompilationOptions.Platform);
            Assert.Equal(ReportDiagnostic.Error, net45Options.CompilationOptions.GeneralDiagnosticOption);
            Assert.Equal(OptimizationLevel.Release, net45Options.CompilationOptions.OptimizationLevel);

            Assert.Equal(new[] { "DEBUG", "TRACE", "K10" }, k10Options.Defines);
            Assert.Equal(ReportDiagnostic.Error, k10Options.CompilationOptions.GeneralDiagnosticOption);
        }

        [Fact]
        public void CompilerOptionsForNonExistantConfigurationReturnsDefaults()
        {
            var project = Project.GetProject(@"
{
    ""frameworks"" : {
        ""net45"":  {
            ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""X"", ""y""], ""platform"": ""x86"", ""warningsAsErrors"": true }
        },
        ""k10"": {
            ""dependencies"": {
            }
        }
    }
}",
"foo",
@"c:\foo\project.json");

            var net451Options = project.GetCompilationSettings("net451");

            Assert.False(net451Options.CompilationOptions.AllowUnsafe);
            Assert.Equal(new[] { "DEBUG", "TRACE" }, net451Options.Defines);
            Assert.Equal(Platform.AnyCpu, net451Options.CompilationOptions.Platform);
        }

        [Fact]
        public void CompilerOptionsForExistantConfigurationReturnsTopLevelIfNotSpecified()
        {
            var project = Project.GetProject(@"
{
    ""compilationOptions"": { ""allowUnsafe"": true },
    ""frameworks"" : {
        ""k10"": {
            ""dependencies"": {
            }
        }
    }
}",
"foo",
@"c:\foo\project.json");

            var k10Options = project.GetCompilationSettings("k10");

            Assert.True(k10Options.CompilationOptions.AllowUnsafe);
            Assert.Equal(new[] { "DEBUG", "TRACE", "K10" }, k10Options.Defines);
            Assert.Equal(Platform.AnyCpu, k10Options.CompilationOptions.Platform);
        }
    }

    public static class ProjectTestExtensions
    {
        public static CompilationSettings GetCompilationSettings(this Project project, string frameworkName)
        {
            var framework = Project.ParseFrameworkName(frameworkName);
            return project.GetCompilerOptions(framework, "Debug")
                          .ToCompilationSettings(framework);
        }
    }
}
