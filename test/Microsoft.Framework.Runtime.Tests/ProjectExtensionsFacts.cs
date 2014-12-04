// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class ProjectExtensionsFacts
    {
        private static readonly string _projectContent = @"{
    ""compilationOptions"": { ""define"": [""GLOBAL""], ""warningsAsErrors"": true },
    ""configurations"": { 
        ""Debug"": {
            ""compilationOptions"": { ""define"": [""TEST_DEBUG"", ""XYZ""], ""allowUnsafe"": true, ""warningsAsErrors"": true }
        },
    },
    ""frameworks"" : {
        ""aspnet50"": {
            ""compilationOptions"": { ""define"": [""TEST_ASPNET50"" ], ""platform"": ""x86"", ""warningsAsErrors"": true }
        },
        
        ""aspnetcore50"": {
            ""compilationOptions"": { ""allowUnsafe"": true, ""define"": [""TEST_ASPNETCORE""], ""platform"": ""x86"", ""warningsAsErrors"": true }
        }
    }
}";
        [Fact]
        public void GetCompilerOptionsIgnoresTargetFrameworkAndConfigurationIfNull()
        {
            // Arrange
            var project = Project.GetProject(_projectContent, "TestProj", "project.json");

            // Act
            var options = project.GetCompilerOptions(targetFramework: null, configurationName: null);

            // Assert
            Assert.Equal(new[] { "GLOBAL" } , options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Null(options.AllowUnsafe);
            Assert.Null(options.Platform);
        }

        [Fact]
        public void GetCompilerOptionsCombinesTargetFrameworkIfNotNull()
        {
            // Arrange
            var project = Project.GetProject(_projectContent, "TestProj", "project.json");
            var targetFramework = Project.ParseFrameworkName("aspnet50");

            // Act
            var options = project.GetCompilerOptions(targetFramework, configurationName: null);

            // Assert
            Assert.Equal(new[] { "GLOBAL", "TEST_ASPNET50", "ASPNET50" }, options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Null(options.AllowUnsafe);
            Assert.Equal("x86", options.Platform);
        }

        [Fact]
        public void GetCompilerOptionsCombinesConfigurationAndTargetFrameworkfNotNull()
        {
            // Arrange
            var project = Project.GetProject(_projectContent, "TestProj", "project.json");
            var targetFramework = Project.ParseFrameworkName("aspnetcore50");

            // Act
            var options = project.GetCompilerOptions(targetFramework, configurationName: "Debug");

            // Assert
            Assert.Equal(new[] { "GLOBAL", "TEST_DEBUG", "XYZ", "TEST_ASPNETCORE", "ASPNETCORE50" }, options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Equal(true, options.AllowUnsafe);
            Assert.Equal("x86", options.Platform);
        }
    }
}