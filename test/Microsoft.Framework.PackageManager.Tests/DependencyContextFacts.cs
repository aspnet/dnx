using System;
using Microsoft.Framework.PackageManager.Packing;
using Xunit;

namespace Microsoft.Framework.PackageManager.Packing.Tests
{
    public class DependencyContextFacts
    {
        [Theory]
        [InlineData("KRE-CLR-x86.1.0.0", "Asp.Net")]
        [InlineData("KRE-CLR-amd64.1.0.0", "Asp.Net")]
        [InlineData("KRE-CoreCLR-x86.1.0.0", "Asp.NetCore")]
        [InlineData("KRE-CoreCLR-amd64.1.0.0", "Asp.NetCore")]
        [InlineData("KRE-Mono.1.0.0", "Asp.Net")]  // Absence of architecture component is allowed for Mono KRE
        [InlineData("KRE-Mono-x86.1.0.0", "Asp.Net")]
        [InlineData("KRE-CLR.1.0.0", null)]
        [InlineData("KRE-CoreCLR-x86", null)]
        [InlineData("KRE-Mono", null)]
        [InlineData("KRE", null)]
        public void GetCorrectFrameworkNameForKREs(string runtimeName, string frameworkIdentifier)
        {
            var frameworkName = DependencyContext.GetFrameworkNameForRuntime(runtimeName);

            Assert.Equal(frameworkIdentifier, frameworkName?.Identifier);
        }
    }
}