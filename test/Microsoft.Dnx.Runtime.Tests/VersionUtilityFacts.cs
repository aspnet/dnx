using System;
using System.Runtime.Versioning;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class VersionUtilityFacts
    {
        [Theory]
        [InlineData("dotnet", ".NETPlatform", "5.0")]
        [InlineData("dotnet10", ".NETPlatform", "1.0")]
        [InlineData("dotnet50", ".NETPlatform", "5.0")]
        [InlineData("dotnet60", ".NETPlatform", "6.0")]
        public void CanParseShortFrameworkNames(string shortName, string longName, string version)
        {
            var fx = VersionUtility.ParseFrameworkName(shortName);
            Assert.Equal(new FrameworkName(longName, Version.Parse(version)), fx);
        }

        [Theory]
        [InlineData(".NETPlatform", "5.0", "dotnet")]
        [InlineData(".NETPlatform", "5.1", "dotnet51")]
        public void ShortFrameworkNamesAreCorrect(string longName, string version, string shortName)
        {
            var fx = new FrameworkName(longName, Version.Parse(version));
            Assert.Equal(shortName, VersionUtility.GetShortFrameworkName(fx));
        }
    }
}
