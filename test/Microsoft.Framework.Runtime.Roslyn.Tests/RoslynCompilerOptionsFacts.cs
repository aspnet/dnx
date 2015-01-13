// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class RoslynCompilerOptionsFacts
    {
        [Fact]
        public void MergingWithNullSkipsNulls()
        {
            // Arrange
            var options = new RoslynCompilerOptions
            {
                Optimize = true
            };

            // Act
            var result = options.Merge(options: null);

            // Assert
            var resultOptions = Assert.IsType<RoslynCompilerOptions>(result);
            Assert.True(resultOptions.Optimize.Value);
        }

        [Fact]
        public void MergingWithOtherOptionsOverwrites()
        {
            // Arrange
            var options = new RoslynCompilerOptions
            {
                AllowUnsafe = false,
                Optimize = true,
                WarningsAsErrors = true,
                LanguageVersion = "x"
            };

            var options2 = new RoslynCompilerOptions
            {
                AllowUnsafe = true,
                Optimize = false,
                WarningsAsErrors = false,
                LanguageVersion = "y",
            };

            // Act
            var result = options.Merge(options2);

            // Assert
            var resultOptions = Assert.IsType<RoslynCompilerOptions>(result);
            Assert.True(resultOptions.AllowUnsafe.Value);
            Assert.False(resultOptions.Optimize.Value);
            Assert.False(resultOptions.WarningsAsErrors.Value);
            Assert.Equal("y", resultOptions.LanguageVersion);
        }

        [Fact]
        public void CombiningConcatsDefines()
        {
            // Arrange
            var options = new RoslynCompilerOptions
            {
                Defines = new[] { "OPT1", "OPT3" }
            };

            var options2 = new RoslynCompilerOptions
            {
                Defines = new[] { "OPT2", "OPT3" }
            };

            // Act
            var result = options.Merge(options2);

            // Assert
            var resultOptions = Assert.IsType<RoslynCompilerOptions>(result);
            Assert.Equal(new[] { "OPT1", "OPT3", "OPT2" }, resultOptions.Defines);
        }
    }
}