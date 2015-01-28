using System;
using Microsoft.Framework.PackageManager.Bundle;
using Xunit;

namespace Microsoft.Framework.PackageManager.Bundle.Tests
{
    public class DependencyContextFacts
    {
        [Theory]
        [InlineData("kre-clr-win-x86.1.0.0", "Asp.Net")]
        [InlineData("kre-clr-win-x64.1.0.0", "Asp.Net")]
        [InlineData("kre-coreclr-win-x86.1.0.0", "Asp.NetCore")]
        [InlineData("kre-coreclr-win-x64.1.0.0", "Asp.NetCore")]
        [InlineData("kre-mono.1.0.0", "Asp.Net")]  // Absence of architecture component is allowed for mono runtime
        [InlineData("kre-mono-x86.1.0.0", "Asp.Net")]
        [InlineData("kre-clr.1.0.0", null)]
        [InlineData("kre-coreclr-win-x86", null)]
        [InlineData("kre-mono", null)]
        [InlineData("kre", null)]
        public void GetCorrectFrameworkNameForRuntimes(string runtimeName, string frameworkIdentifier)
        {
            var frameworkName = DependencyContext.GetFrameworkNameForRuntime(runtimeName);

            Assert.Equal(frameworkIdentifier, frameworkName?.Identifier);
        }
    }
}