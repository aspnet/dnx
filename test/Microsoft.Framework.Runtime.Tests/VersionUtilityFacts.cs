using System;
using System.Runtime.Versioning;
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
        [InlineData("net45", "aspnet452", false)]
        [InlineData("aspnet50", "net45", true)]
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
        // Temporary until our dependencies update
        [InlineData("aspnetcore50", "portable-net45+win8", true)]
        [InlineData("aspnetcore50", "portable-net451+win81", true)]
        [InlineData("aspnetcore50", "portable-net40+sl5+win8", false)]
        [InlineData("aspnetcore50", "portable-net45+win8", true)]
        [InlineData("aspnetcore50", "portable-net451+win81", true)]
        [InlineData("aspnetcore50", "portable-net40+sl5+win8", false)]

        // Tests for aspnet -> dnx rename
        [InlineData("dnx451", "aspnet50", true)]
        [InlineData("dnx452", "dnx451", true)]
        [InlineData("dnx451", "net45", true)]
        [InlineData("aspnet50", "dnx451", false)]
        [InlineData("net45", "dnx451", false)]

        [InlineData("dnxcore50", "aspnetcore50", true)]
        [InlineData("aspnetcore50", "dnxcore50", false)]
        // Portable stuff?

        [InlineData("dnxcore50", "portable-net40+win8+dnxcore50", true)]
        [InlineData("dnxcore50", "portable-net40+win8+aspnetcore50", true)]
        [InlineData("dnxcore50", "portable-net45+win8", true)]
        [InlineData("dnxcore50", "portable-net451+win81", true)]
        [InlineData("dnxcore50", "portable-net40+sl5+win8", false)]
        [InlineData("dnxcore50", "portable-net45+win8", true)]
        [InlineData("dnxcore50", "portable-net451+win81", true)]
        [InlineData("dnxcore50", "portable-net40+sl5+win8", false)]

        // NetPortable50
        [InlineData("netportable50", "netportable50", true)]
        [InlineData("dnxcore50", "netportable50", true)]
        [InlineData("aspnetcore50", "netportable50", true)]
        [InlineData("dnx451", "netportable50", true)]
        [InlineData("dnx46", "netportable50", true)]
        [InlineData("net451", "netportable50", false)]
        [InlineData("net40", "netportable50", false)]
        [InlineData("net46", "netportable50", true)]
        [InlineData("sl20", "netportable50", false)]
        [InlineData("netportable50", "portable-net40+sl5+win8", false)]
        [InlineData("netportable50", "portable-net45+win8", true)]
        [InlineData("netportable50", "portable-net451+win81", true)]
        [InlineData("netportable50", "portable-net451+win8+core50", true)]
        [InlineData("netportable50", "portable-net451+win8+dnxcore50", true)]
        [InlineData("netportable50", "portable-net451+win8+aspnetcore50", true)]

        // Old-world Portable doesn't support netportable50
        [InlineData("portable-net40+sl5+win8", "netportable50", false)]
        [InlineData("portable-net45+win8", "netportable50", false)]
        [InlineData("portable-net451+win81", "netportable50", false)]
        [InlineData("portable-net451+win8+core50", "netportable50", false)]
        [InlineData("portable-net451+win8+dnxcore50", "netportable50", false)]
        [InlineData("portable-net451+win8+aspnetcore50", "netportable50", false)]
        public void FrameworksAreCompatible(string project, string package, bool compatible)
        {
            var frameworkName1 = VersionUtility.ParseFrameworkName(project);
            var frameworkName2 = VersionUtility.ParseFrameworkName(package);

            var result = VersionUtility.IsCompatible(frameworkName1, frameworkName2);

            Assert.Equal(compatible, result);
        }

        [Theory]
        [InlineData(".NETPortable", "0.0", "net45+win8", "portable-net45+win80")]
        [InlineData(".NETPortable", "4.2", "net45", "portable-net45")] // Portable version numbers < 5.0 didn't matter
        [InlineData(".NETPortable", "5.0", null, "netportable50")]
        [InlineData(".NETPortable", "5.1", null, "netportable51")]
        [InlineData(".NETPortable", "6.0", null, "netportable60")]
        public void ShortFrameworkNamesAreCorrect(string longName, string version, string profile, string shortName)
        {
            var fx = new FrameworkName(longName, Version.Parse(version), profile);
            Assert.Equal(shortName, VersionUtility.GetShortFrameworkName(fx));
        }
        
        [Theory]
        [InlineData(".NETPortable1.0", true)]
        [InlineData(".NETPortable4.9", true)]
        [InlineData(".NETPortable5.0", false)]
        public void SkipPortablePartValidationWhenVersionIsHigherThanFive(string frameworkName, bool throwException)
        {
            if (throwException)
            {
                Assert.Throws<ArgumentException>(() => VersionUtility.ParseFrameworkName(frameworkName));
            }
            else
            {
                var framework = VersionUtility.ParseFrameworkName(frameworkName);
                Assert.Equal(".NETPortable", framework.Identifier);
            }
        }
    }
}
