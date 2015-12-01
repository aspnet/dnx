// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Dnx.Runtime.Helpers;
using Microsoft.Dnx.Runtime.Internal;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class ProjectExtensionsFacts
    {
        [Fact]
        public void DefaultDesktopCompilationSettings()
        {
            var project = ProjectUtilities.GetProject(
@"{

}",
"foo",
@"c\foo\project.json");

            var settings = project.GetCompilationSettings("net45");

            Assert.NotNull(settings);
            Assert.NotNull(settings.Defines);
            Assert.Equal(new[] { "DEBUG", "TRACE" }, settings.Defines);
            Assert.Equal(LanguageVersion.CSharp6, settings.LanguageVersion);
#if DNX451
            Assert.IsType<DesktopAssemblyIdentityComparer>(settings.CompilationOptions.AssemblyIdentityComparer);
#else
            Assert.IsType<AssemblyIdentityComparer>(settings.CompilationOptions.AssemblyIdentityComparer);
#endif
            Assert.Equal(OutputKind.DynamicallyLinkedLibrary, settings.CompilationOptions.OutputKind);
        }

        [Fact]
        public void ChangingLanguageVersionIsEffective()
        {
            var project = ProjectUtilities.GetProject(
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
        [InlineData("dnxcore50", "DEBUG,TRACE")]
        public void DefaultDefines(string shortName, string define)
        {
            var project = ProjectUtilities.GetProject(
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
            var project = ProjectUtilities.GetProject(
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
            var project = ProjectUtilities.GetProject(@"
{
    ""frameworks"" : {
        ""net45"":  {
            ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""X"", ""y""], ""platform"": ""x86"", ""warningsAsErrors"": true, ""optimize"": true, ""debugSymbols"": ""none"" }
        },
        ""dnxcore50"": {
            ""compilationOptions"": { ""warningsAsErrors"": true, ""debugSymbols"": ""pdbOnly"" }
        }
    }
}",
"foo",
@"c:\foo\project.json");

            var net45Options = project.GetCompilationSettings("net45");
            var dnxcore50Options = project.GetCompilationSettings("dnxcore50");

            Assert.True(net45Options.CompilationOptions.AllowUnsafe);
            Assert.Equal(new[] { "DEBUG", "TRACE", "X", "y", "NET45" }, net45Options.Defines);
            Assert.Equal(Platform.X86, net45Options.CompilationOptions.Platform);
            Assert.Equal(ReportDiagnostic.Error, net45Options.CompilationOptions.GeneralDiagnosticOption);
            Assert.Equal(OptimizationLevel.Release, net45Options.CompilationOptions.OptimizationLevel);

            Assert.Equal(new[] { "DEBUG", "TRACE", "DNXCORE50" }, dnxcore50Options.Defines);
            Assert.Equal(ReportDiagnostic.Error, dnxcore50Options.CompilationOptions.GeneralDiagnosticOption);
        }

        [Fact]
        public void CompilerOptionsForNonExistantConfigurationReturnsDefaults()
        {
            var project = ProjectUtilities.GetProject(@"
{
    ""frameworks"" : {
        ""net45"":  {
            ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""X"", ""y""], ""platform"": ""x86"", ""warningsAsErrors"": true }
        },
        ""dnxcore50"": {
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
            var project = ProjectUtilities.GetProject(@"
{
    ""compilationOptions"": { ""allowUnsafe"": true },
    ""frameworks"" : {
        ""dnxcore50"": {
            ""dependencies"": {
            }
        }
    }
}",
"foo",
@"c:\foo\project.json");

            var dnxcore50Options = project.GetCompilationSettings("dnxcore50");

            Assert.True(dnxcore50Options.CompilationOptions.AllowUnsafe);
            Assert.Equal(new[] { "DEBUG", "TRACE", "DNXCORE50" }, dnxcore50Options.Defines);
            Assert.Equal(Platform.AnyCpu, dnxcore50Options.CompilationOptions.Platform);
        }
    }

    public static class ProjectTestExtensions
    {
        public static CompilationSettings GetCompilationSettings(this Project project, string frameworkName)
        {
            var framework = FrameworkNameHelper.ParseFrameworkName(frameworkName);
            return project.GetCompilerOptions(framework, "Debug")
                          .ToCompilationSettings(framework, project.ProjectDirectory);
        }
    }
}
