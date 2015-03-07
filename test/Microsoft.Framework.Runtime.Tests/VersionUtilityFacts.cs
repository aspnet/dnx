using System;
using NuGet;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class VersionUtilityFacts
    {
        [Theory]
        [InlineData("net45", "aspnet50", false)]
        [InlineData("aspnet50", "net45", true)]
        [InlineData("aspnetcore50", "k10", false)]
        [InlineData("k10", "aspnetcore50", false)]
        [InlineData("netcore45", "aspnetcore50", false)]
        [InlineData("win8", "aspnetcore50", false)]
        [InlineData("win81", "aspnetcore50", false)]
        [InlineData("aspnetcore50", "netcore45", false)]
        [InlineData("aspnetcore50", "win8", false)]
        [InlineData("aspnetcore50", "win81", false)]
        [InlineData("aspnetcore50", "portable-net40+win8+aspnetcore50", true)]
        [InlineData("net45", "dnx452", false)]
        [InlineData("dnx452", "net45", true)]
        [InlineData("dnx452", "net45", true)]
        [InlineData("dnxcore50", "k10", false)]
        [InlineData("k10", "dnxcore50", false)]
        [InlineData("netcore45", "dnxcore50", false)]
        [InlineData("win8", "dnxcore50", false)]
        [InlineData("win81", "dnxcore50", false)]
        [InlineData("dnxcore50", "netcore45", false)]
        [InlineData("dnxcore50", "win8", false)]
        [InlineData("dnxcore50", "win81", false)]
        [InlineData("dnxcore50", "portable-net40+win8+dnxcore50", true)]
        // Temporary until our dependencies update
        [InlineData("aspnetcore50", "portable-net45+win8", true)]
        [InlineData("aspnetcore50", "portable-net451+win81", true)]
        [InlineData("aspnetcore50", "portable-net40+sl5+win8", false)]
        [InlineData("dnxcore50", "portable-net45+win8", true)]
        [InlineData("dnxcore50", "portable-net451+win81", true)]
        [InlineData("dnxcore50", "portable-net40+sl5+win8", false)]

        // Tests for aspnet -> dnx rename
        // DNX packages installed into ASPNET50 projects? OK!
        [InlineData("aspnet50", "dnx452", true)]
        [InlineData("aspnetcore50", "dnxcore50", true)]
        
        // ASPNET50 packages into DNX projects? Sure!
        [InlineData("dnx452", "aspnet50", true)]
        [InlineData("dnxcore50", "aspnetcore50", true)]

        // Portable stuff?
        [InlineData("dnxcore50", "portable-net40+win8+aspnetcore50", true)]
        public void FrameworksAreCompatible(string projectTargetFramework, string packageTargetFramework, bool compatible)
        {
            var frameworkName1 = VersionUtility.ParseFrameworkName(projectTargetFramework);
            var frameworkName2 = VersionUtility.ParseFrameworkName(packageTargetFramework);

            var result = VersionUtility.IsCompatible(frameworkName1, frameworkName2);

            Assert.Equal(compatible, result);
        }
    }
}