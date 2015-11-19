// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Runtime.Helpers;
using Microsoft.Dnx.Runtime.Internal;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class ProjectExtensionsFacts
    {
        private static readonly string _projectContent = @"{
    ""compilationOptions"": { ""define"": [""GLOBAL""], ""warningsAsErrors"": true },
    ""configurations"": { 
        ""Debug"": {
            ""compilationOptions"": { ""define"": [""TEST_DEBUG"", ""XYZ""], ""allowUnsafe"": true, ""warningsAsErrors"": true }
        }
    },
    ""frameworks"" : {
        ""dnx451"": {
            ""compilationOptions"": { ""define"": [""TEST_DNX451"" ], ""platform"": ""x86"", ""warningsAsErrors"": true }
        },
        ""dnxcore50"": {
            ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""TEST_ASPNETCORE""], ""platform"": ""x86"", ""warningsAsErrors"": true }
        }
    }
}";
        [Fact]
        public void GetCompilerOptionsIgnoresTargetFrameworkAndConfigurationIfNull()
        {
            // Arrange
            var project = ProjectUtilities.GetProject(_projectContent, "TestProj", "project.json");

            // Act
            var options = project.GetCompilerOptions(targetFramework: null, configurationName: null);

            // Assert
            Assert.Equal(new[] { "GLOBAL" }, options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Null(options.AllowUnsafe);
            Assert.Null(options.Platform);
        }

        [Fact]
        public void GetCompilerOptionsCombinesTargetFrameworkIfNotNull()
        {
            // Arrange
            var project = ProjectUtilities.GetProject(_projectContent, "TestProj", "project.json");
            var targetFramework = FrameworkNameHelper.ParseFrameworkName("dnx451");

            // Act
            var options = project.GetCompilerOptions(targetFramework, configurationName: null);

            // Assert
            Assert.Equal(new[] { "GLOBAL", "TEST_DNX451", "DNX451" }, options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Null(options.AllowUnsafe);
            Assert.Equal("x86", options.Platform);
        }

        [Theory]
        [InlineData("net40", "NET40")]
        [InlineData(".NETFramework,Version=v4.0", "NET40")]
        [InlineData(".NETFramework,Version=v4.0,Profile=Client", "NET40_CLIENT")]
        [InlineData("DOTNET5.1", "DOTNET5_1")]
        [InlineData(".NETPortable,Version=v4.5,Profile=Profile123", null)]
        [InlineData("portable-net45+win81", null)]
        public void GetCompilerOptionsGeneratesTFMDefineForShortName(string tfm, string define)
        {
            // Arrange
            var projectContent = @"{ ""frameworks"": { """ + tfm + @""": {} } }";
            var project = ProjectUtilities.GetProject(projectContent, "TestProj", "project.json");
            var targetFramework = FrameworkNameHelper.ParseFrameworkName(tfm);

            // Act
            var options = project.GetCompilerOptions(targetFramework, configurationName: "Debug");

            // Assert
            var expectedDefines = define == null ? new[] { "DEBUG", "TRACE" } : new[] { "DEBUG", "TRACE", define };
            Assert.Equal(expectedDefines, options.Defines);
        }

        [Fact]
        public void GetCompilerOptionsCombinesConfigurationAndTargetFrameworkfNotNull()
        {
            // Arrange
            var project = ProjectUtilities.GetProject(_projectContent, "TestProj", "project.json");
            var targetFramework = FrameworkNameHelper.ParseFrameworkName("dnxcore50");

            // Act
            var options = project.GetCompilerOptions(targetFramework, configurationName: "Debug");

            // Assert
            Assert.Equal(new[] { "GLOBAL", "TEST_DEBUG", "XYZ", "TEST_ASPNETCORE", "DNXCORE50" }, options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Equal(true, options.AllowUnsafe);
            Assert.Equal("x86", options.Platform);
        }
    }
}
