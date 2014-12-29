// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilerOptionsExtensionsFacts
    {
        [Fact]
        public void DefaultDesktopCompilationSettings()
        {
            // Arrange
            var options = new RoslynCompilerOptions
            {
                Defines = new[] { "DEBUG", "TRACE" },
            };

            // Act
            var settings = options.ToCompilationSettings(ProjectReader.ParseFrameworkName("net45"));

            // Assert
            Assert.NotNull(settings);
            Assert.NotNull(settings.Defines);
            Assert.Equal(new[] { "DEBUG", "TRACE" }, settings.Defines);
            Assert.Equal(LanguageVersion.CSharp6, settings.LanguageVersion);
            Assert.IsType<DesktopAssemblyIdentityComparer>(settings.CompilationOptions.AssemblyIdentityComparer);
            Assert.Equal(OutputKind.DynamicallyLinkedLibrary, settings.CompilationOptions.OutputKind);
        }

        [Theory]
        [InlineData("CSharp3", LanguageVersion.CSharp3)]
        [InlineData("csharp5", LanguageVersion.CSharp5)]
        public void ToCompilationSettingsParsesLanguageVersion(string languageVersion, LanguageVersion expected)
        {
            // Arrange
            var options = new RoslynCompilerOptions
            {
                LanguageVersion = languageVersion
            };

            // Act
            var settings = options.ToCompilationSettings(ProjectReader.ParseFrameworkName("net45"));

            // Assert
            Assert.Equal(expected, settings.LanguageVersion);
        }

        [Theory]
        [InlineData(null, OptimizationLevel.Debug)]
        [InlineData(false, OptimizationLevel.Debug)]
        [InlineData(true, OptimizationLevel.Release)]
        public void ToCompilationSettingsSetsOptimizationLevel(bool? optimize, OptimizationLevel expected)
        {
            // Arrange
            var options = new RoslynCompilerOptions
            {
                Optimize = optimize,
            };

            // Act
            var settings = options.ToCompilationSettings(ProjectReader.ParseFrameworkName("net45"));

            // Assert
            Assert.Equal(expected, settings.CompilationOptions.OptimizationLevel);
        }

        [Theory]
        [InlineData(null, Platform.AnyCpu)]
        [InlineData("AnyCPU", Platform.AnyCpu)]
        [InlineData("x86", Platform.X86)]
        [InlineData("X64", Platform.X64)]
        public void ToCompilationSettingsSetsPlatform(string platform, Platform expected)
        {
            // Arrange
            var options = new RoslynCompilerOptions
            {
                Platform = platform
            };

            // Act
            var settings = options.ToCompilationSettings(ProjectReader.ParseFrameworkName("net45"));

            // Assert
            Assert.Equal(expected, settings.CompilationOptions.Platform);
        }

        [Fact]
        public void ToCompilationSettingsSetsWarningAsErrorsAndAllowUnsafe()
        {
            // Arrange
            var options = new RoslynCompilerOptions
            {
                AllowUnsafe = true,
                WarningsAsErrors = true
            };

            // Act
            var settings = options.ToCompilationSettings(ProjectReader.ParseFrameworkName("net45"));

            // Assert
            Assert.True(settings.CompilationOptions.AllowUnsafe);
            Assert.Equal(ReportDiagnostic.Error, settings.CompilationOptions.GeneralDiagnosticOption);
        }
    }
}