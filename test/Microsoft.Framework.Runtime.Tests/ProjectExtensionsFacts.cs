// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Runtime.Loader;
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
            var reader = GetProjectReader();
            var project = reader.GetProject(_projectContent, "TestProj", "project.json");

            // Act
            var rawOptions = project.GetCompilerOptions(targetFramework: null, configurationName: null);

            // Assert
            var options = Assert.IsType<RoslynCompilerOptions>(rawOptions);
            Assert.Equal(new[] { "GLOBAL" } , options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Null(options.AllowUnsafe);
            Assert.Null(options.Platform);
        }

        [Fact]
        public void GetCompilerOptionsCombinesTargetFrameworkIfNotNull()
        {
            // Arrange
            var reader = GetProjectReader();
            var project = reader.GetProject(_projectContent, "TestProj", "project.json");
            var targetFramework = ProjectReader.ParseFrameworkName("aspnet50");

            // Act
            var rawOptions = project.GetCompilerOptions(targetFramework, configurationName: null);

            // Assert
            var options = Assert.IsType<RoslynCompilerOptions>(rawOptions);
            Assert.Equal(new[] { "GLOBAL", "TEST_ASPNET50", "ASPNET50" }, options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Null(options.AllowUnsafe);
            Assert.Equal("x86", options.Platform);
        }

        [Fact]
        public void GetCompilerOptionsCombinesConfigurationAndTargetFrameworkfNotNull()
        {
            // Arrange
            var reader = GetProjectReader();
            var project = reader.GetProject(_projectContent, "TestProj", "project.json");
            var targetFramework = ProjectReader.ParseFrameworkName("aspnetcore50");

            // Act
            var rawOptions = project.GetCompilerOptions(targetFramework, configurationName: "Debug");

            // Assert
            var options = Assert.IsType<RoslynCompilerOptions>(rawOptions);
            Assert.Equal(new[] { "GLOBAL", "TEST_DEBUG", "XYZ", "TEST_ASPNETCORE", "ASPNETCORE50" }, options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Equal(true, options.AllowUnsafe);
            Assert.Equal("x86", options.Platform);
        }

        private static ProjectReader GetProjectReader()
        {
            return new ProjectReader(LoadContextAccessor.Instance.Default);
        }
    }
}