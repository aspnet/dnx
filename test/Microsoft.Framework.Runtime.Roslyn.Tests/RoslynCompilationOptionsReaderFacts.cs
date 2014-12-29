// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Framework.Runtime.Roslyn.Tests
{
    public class RoslynCompilationOptionsReaderFacts
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReadCompilationOption_ReturnsEmptyCompilationOptionsByDefault(string content)
        {
            // Arrange
            var reader = new RoslynCompilerOptionsReader();

            // Act
            var options = reader.ReadCompilerOptions(content);

            // Assert
            var roslynCompilerOptions = Assert.IsType<RoslynCompilerOptions>(options);
            Assert.Null(roslynCompilerOptions.AllowUnsafe);
            Assert.Null(roslynCompilerOptions.Defines);
            Assert.Null(roslynCompilerOptions.LanguageVersion);
            Assert.Null(roslynCompilerOptions.Optimize);
            Assert.Null(roslynCompilerOptions.Platform);
            Assert.Null(roslynCompilerOptions.WarningsAsErrors);
        }

        [Fact]
        public void ReadCompilationOption_ReadsCompilationOptionsNode()
        {
            // Arrange
            var content =
@"{
   allowUnsafe: true,
   define: [ ""X"" ],
   languageVersion: ""CSharp5"",
   optimize: ""false"",
   platform: ""x86"",
   warningsAsErrors: ""true""
}";
            var reader = new RoslynCompilerOptionsReader();

            // Act
            var options = reader.ReadCompilerOptions(content);

            // Assert
            var roslynCompilerOptions = Assert.IsType<RoslynCompilerOptions>(options);
            Assert.True(roslynCompilerOptions.AllowUnsafe.Value);
            Assert.Equal(new[] { "X" }, roslynCompilerOptions.Defines);
            Assert.Equal("CSharp5", roslynCompilerOptions.LanguageVersion);
            Assert.False(roslynCompilerOptions.Optimize.Value);
            Assert.Equal("x86", roslynCompilerOptions.Platform);
            Assert.True(roslynCompilerOptions.WarningsAsErrors.Value);
        }

        [Theory]
        [InlineData(null, "aspnet50", "ASPNET50")]
        [InlineData("", "ASPNETCORE50", "ASPNETCORE50")]
        [InlineData("", "net45", "NET45")]
        public void GetFrameworkCompilationOption_AddsFrameworkDefinesByDefault(string content,
                                                                                string targetFramework,
                                                                                string expectedFramework)
        {
            // Arrange
            var reader = new RoslynCompilerOptionsReader();

            // Act
            var options = reader.ReadFrameworkCompilerOptions(content,
                                                              targetFramework,
                                                              ProjectReader.ParseFrameworkName(targetFramework));

            // Assert
            var roslynCompilerOptions = Assert.IsType<RoslynCompilerOptions>(options);
            Assert.Null(roslynCompilerOptions.AllowUnsafe);
            Assert.Equal(new[] { expectedFramework }, roslynCompilerOptions.Defines);
            Assert.Null(roslynCompilerOptions.LanguageVersion);
            Assert.Null(roslynCompilerOptions.Optimize);
            Assert.Null(roslynCompilerOptions.Platform);
            Assert.Null(roslynCompilerOptions.WarningsAsErrors);
        }

        [Fact]
        public void GetFrameworkCompilationOption_AppensTargetFrameworkToSpecifiedValues()
        {
            // Arrange
            var content =
@"{
   allowUnsafe: true,
   define: [ ""X"" ],
   optimize: ""false"",
   platform: ""AnyCPU"",
   warningsAsErrors: ""true""
}";
            var reader = new RoslynCompilerOptionsReader();

            // Act
            var options = reader.ReadFrameworkCompilerOptions(content,
                                                              "aspnet50",
                                                              ProjectReader.ParseFrameworkName("aspnet50"));

            // Assert
            var roslynCompilerOptions = Assert.IsType<RoslynCompilerOptions>(options);
            Assert.True(roslynCompilerOptions.AllowUnsafe.Value);
            Assert.Equal(new[] { "X", "ASPNET50" }, roslynCompilerOptions.Defines);
            Assert.Null(roslynCompilerOptions.LanguageVersion);
            Assert.False(roslynCompilerOptions.Optimize.Value);
            Assert.Equal("AnyCPU", roslynCompilerOptions.Platform);
            Assert.True(roslynCompilerOptions.WarningsAsErrors.Value);
        }

        [Theory]
        [InlineData(null, "Debug", false)]
        [InlineData("", "release", true)]
        [InlineData("{ warningAsErrors: true }", "Release", true)]
        public void ReadConfigurationCompilationOption_AddsDefaults(string content, string configuration, bool optimize)
        {
            // Arrange
            var reader = new RoslynCompilerOptionsReader();

            // Act
            var options = reader.ReadConfigurationCompilerOptions(content,
                                                                  configuration);

            // Assert
            var roslynCompilerOptions = Assert.IsType<RoslynCompilerOptions>(options);
            Assert.Equal(new[] { configuration.ToUpperInvariant(), "TRACE" }, roslynCompilerOptions.Defines);
            Assert.Equal(optimize, roslynCompilerOptions.Optimize);
        }

        [Fact]
        public void ReadConfigurationCompilationOption_DoesNotAddDefaultsIfSpecifiedInCompilationOptions()
        {
            // Arrange
            var reader = new RoslynCompilerOptionsReader();

            // Act
            var options = reader.ReadConfigurationCompilerOptions("{ define: [], optimize: false }",
                                                                  "Release");

            // Assert
            var roslynCompilerOptions = Assert.IsType<RoslynCompilerOptions>(options);
            Assert.Empty(roslynCompilerOptions.Defines);
            Assert.False(roslynCompilerOptions.Optimize.Value);
        }

        [Fact]
        public void ReadConfigurationCompilationOption_ReadsCompilationOptionsNode()
        {
            // Arrange
            var content =
@"{
   allowUnsafe: true,
   define: [ ""X"" ],
   languageVersion: ""CSharp5"",
   platform: ""x86"",
   warningsAsErrors: ""true""
}";
            var reader = new RoslynCompilerOptionsReader();

            // Act
            var options = reader.ReadConfigurationCompilerOptions(content, "Release");

            // Assert
            var roslynCompilerOptions = Assert.IsType<RoslynCompilerOptions>(options);
            Assert.True(roslynCompilerOptions.AllowUnsafe.Value);
            Assert.Equal(new[] { "X" }, roslynCompilerOptions.Defines);
            Assert.Equal("CSharp5", roslynCompilerOptions.LanguageVersion);
            Assert.True(roslynCompilerOptions.Optimize.Value);
            Assert.Equal("x86", roslynCompilerOptions.Platform);
            Assert.True(roslynCompilerOptions.WarningsAsErrors.Value);
        }
    }
}