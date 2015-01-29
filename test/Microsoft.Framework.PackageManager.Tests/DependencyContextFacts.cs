// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.PackageManager.Bundle;
using Xunit;

namespace Microsoft.Framework.PackageManager.Bundle.Tests
{
    public class DependencyContextFacts
    {
        [Theory]
        [InlineData("dotnet-clr-win-x86.1.0.0", "Asp.Net")]
        [InlineData("dotnet-clr-win-x64.1.0.0", "Asp.Net")]
        [InlineData("dotnet-coreclr-win-x86.1.0.0", "Asp.NetCore")]
        [InlineData("dotnet-coreclr-win-x64.1.0.0", "Asp.NetCore")]
        [InlineData("dotnet-mono.1.0.0", "Asp.Net")]  // Absence of architecture component is allowed for mono runtime
        [InlineData("dotnet-mono-x86.1.0.0", "Asp.Net")]
        [InlineData("dotnet-clr.1.0.0", null)]
        [InlineData("dotnet-coreclr-win-x86", null)]
        [InlineData("dotnet-mono", null)]
        [InlineData("dotnet", null)]
        public void GetCorrectFrameworkNameForRuntimes(string runtimeName, string frameworkIdentifier)
        {
            var frameworkName = DependencyContext.GetFrameworkNameForRuntime(runtimeName);

            Assert.Equal(frameworkIdentifier, frameworkName?.Identifier);
        }
    }
}