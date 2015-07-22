// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Runtime.Helpers;
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
            var project = Project.GetProject(_projectContent, "TestProj", "project.json");

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
            var project = Project.GetProject(_projectContent, "TestProj", "project.json");
            var targetFramework = FrameworkNameHelper.ParseFrameworkName("dnx451");

            // Act
            var options = project.GetCompilerOptions(targetFramework, configurationName: null);

            // Assert
            Assert.Equal(new[] { "GLOBAL", "TEST_DNX451", "DNX451" }, options.Defines);
            Assert.Equal(true, options.WarningsAsErrors);
            Assert.Null(options.AllowUnsafe);
            Assert.Equal("x86", options.Platform);
        }

        [Fact]
        public void GetCompilerOptionsCombinesConfigurationAndTargetFrameworkfNotNull()
        {
            // Arrange
            var project = Project.GetProject(_projectContent, "TestProj", "project.json");
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
