using System;
using NuGet;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class VersionUtilityFacts
    {
        [Theory]
        [InlineData("aspnetcore50", "k10", true)]
        [InlineData("k10", "aspnetcore50", true)]
        [InlineData("aspnet50", "net45", true)]
        [InlineData("net45", "aspnet50", false)]
        public void FrameworksAreCompatible(string targetFramework1, string targetFramework2, bool compatible)
        {
            var frameworkName1 = VersionUtility.ParseFrameworkName(targetFramework1);
            var frameworkName2 = VersionUtility.ParseFrameworkName(targetFramework2);

            var result = VersionUtility.IsCompatible(frameworkName1, frameworkName2);

            Assert.Equal(compatible, result);
        }
    }
}