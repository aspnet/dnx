// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class CompilerOptionsFacts
    {
        [Fact]
        public void CombiningWithNullSkipsNulls()
        {
            var options = new CompilerOptions
            {
                Optimize = true
            };

            var result = CompilerOptions.Combine(options, null);

            Assert.True(result.Optimize.Value);
        }

        [Fact]
        public void CombiningWithOtherOptionsOverwrites()
        {
            var options = new CompilerOptions
            {
                AllowUnsafe = false,
                Optimize = true,
                WarningsAsErrors = true,
                LanguageVersion = "x"
            };

            var options2 = new CompilerOptions
            {
                AllowUnsafe = true,
                Optimize = false,
                WarningsAsErrors = false,
                LanguageVersion = "y",
            };

            var result = CompilerOptions.Combine(options, options2);

            Assert.True(result.AllowUnsafe.Value);
            Assert.False(result.Optimize.Value);
            Assert.False(result.WarningsAsErrors.Value);
            Assert.Equal("y", result.LanguageVersion);
        }

        [Fact]
        public void CombiningConcatsDefines()
        {
            var options = new CompilerOptions
            {
                Defines = new[] { "OPT1" }
            };

            var options2 = new CompilerOptions
            {
                Defines = new[] { "OPT2" }
            };

            var result = CompilerOptions.Combine(options, options2);

            Assert.Equal(new[] { "OPT1", "OPT2" }, result.Defines);
        }
    }
}