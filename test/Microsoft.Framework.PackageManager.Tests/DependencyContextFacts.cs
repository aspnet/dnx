// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.PackageManager.Bundle;
using Microsoft.Framework.Runtime;
using Xunit;

namespace Microsoft.Framework.PackageManager.Bundle.Tests
{
    public class DependencyContextFacts
    {
        [Theory]
        [InlineData(Constants.RuntimeNamePrefix + "clr-win-x86.1.0.0", "Asp.Net")]
        [InlineData(Constants.RuntimeNamePrefix + "clr-win-x64.1.0.0", "Asp.Net")]
        [InlineData(Constants.RuntimeNamePrefix + "coreclr-win-x86.1.0.0", "Asp.NetCore")]
        [InlineData(Constants.RuntimeNamePrefix + "coreclr-win-x64.1.0.0", "Asp.NetCore")]
        [InlineData(Constants.RuntimeNamePrefix + "mono.1.0.0", "Asp.Net")]  // Absence of architecture component is allowed for mono runtime
        [InlineData(Constants.RuntimeNamePrefix + "mono-x86.1.0.0", "Asp.Net")]
        [InlineData(Constants.RuntimeNamePrefix + "clr.1.0.0", null)]
        [InlineData(Constants.RuntimeNamePrefix + "coreclr-win-x86", null)]
        [InlineData(Constants.RuntimeNamePrefix + "mono", null)]
        [InlineData(Constants.RuntimeNamePrefix, null)]
        public void GetCorrectFrameworkNameForRuntimes(string runtimeName, string frameworkIdentifier)
        {
            var frameworkName = DependencyContext.GetFrameworkNameForRuntime(runtimeName);

            Assert.Equal(frameworkIdentifier, frameworkName?.Identifier);
        }
    }
}