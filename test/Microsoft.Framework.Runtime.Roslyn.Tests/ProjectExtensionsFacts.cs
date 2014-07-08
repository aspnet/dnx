// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            Assert.Equal(new[] { "NET45" }, settings.Defines);
            Assert.Equal(LanguageVersion.CSharp6, settings.LanguageVersion);
            Assert.IsType<DesktopAssemblyIdentityComparer>(settings.CompilationOptions.AssemblyIdentityComparer);
            Assert.Equal(DebugInformationKind.Full, settings.CompilationOptions.DebugInformationKind);
            Assert.Equal(OutputKind.DynamicallyLinkedLibrary, settings.CompilationOptions.OutputKind);
        }

        [Fact]
        public void ChangingLanguageVersionIsEffective()
        {
            var project = Project.GetProject(
@"{
    ""compilationOptions"": { ""languageVersion"" : ""experimental"" }
}",
"foo",
@"c\foo\project.json");

            var settings = project.GetCompilationSettings("net45");

            Assert.Equal(LanguageVersion.Experimental, settings.LanguageVersion);
        }

        [Theory]
        [InlineData("net45", "NET45")]
        [InlineData("k10", "K10")]
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
            Assert.Equal(new[] { define }, settings.Defines);
        }

        [Fact]
        public void DefinesFromTopLevelAreCombinedWithFrameworkSpecific()
        {
            var project = Project.GetProject(
@"{
    ""compilationOptions"": { ""define"": [""X""] },
    ""configurations"": {
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
            Assert.Equal(new[] { "X", "NET45", "Something" }, settings.Defines);
        }

        [Fact]
        public void CompilerOptionsAreSetPerConfiguration()
        {
            var project = Project.GetProject(@"
{
    ""configurations"" : {
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
            Assert.Equal(new[] { "X", "y" }, net45Options.Defines);
            Assert.Equal(Platform.X86, net45Options.CompilationOptions.Platform);
            Assert.Equal(ReportDiagnostic.Error, net45Options.CompilationOptions.GeneralDiagnosticOption);
            Assert.True(net45Options.CompilationOptions.Optimize);
            Assert.Equal(DebugInformationKind.None, net45Options.CompilationOptions.DebugInformationKind);

            Assert.Equal(new[] { "K10" }, k10Options.Defines);
            Assert.Equal(ReportDiagnostic.Error, k10Options.CompilationOptions.GeneralDiagnosticOption);
            Assert.Equal(DebugInformationKind.PDBOnly, k10Options.CompilationOptions.DebugInformationKind);
        }

        [Fact]
        public void CompilerOptionsForNonExistantConfigurationReturnsDefaults()
        {
            var project = Project.GetProject(@"
{
    ""configurations"" : {
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

            var net451ptions = project.GetCompilationSettings("net451");

            Assert.False(net451ptions.CompilationOptions.AllowUnsafe);
            Assert.Equal(new[] { "NET451" }, net451ptions.Defines);
            Assert.Equal(Platform.AnyCpu, net451ptions.CompilationOptions.Platform);
            Assert.Equal(DebugInformationKind.Full, net451ptions.CompilationOptions.DebugInformationKind);
        }

        [Fact]
        public void CompilerOptionsForExistantConfigurationReturnsTopLevelIfNotSpecified()
        {
            var project = Project.GetProject(@"
{
    ""compilationOptions"": { ""allowUnsafe"": true },
    ""configurations"" : {
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
            Assert.Equal(new[] { "K10" }, k10Options.Defines);
            Assert.Equal(Platform.AnyCpu, k10Options.CompilationOptions.Platform);
            Assert.Equal(DebugInformationKind.Full, k10Options.CompilationOptions.DebugInformationKind);
        }
    }
}
