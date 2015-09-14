using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Helpers;
using NuGet;
using Xunit;
using Microsoft.AspNet.Testing;
using Microsoft.AspNet.Testing.xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class VersionUtilityFacts
    {
        [Theory]
        // Compat rules that we have just because we need it for now
        [InlineData("dotnet", "portable-net45+win8", true)]
        [InlineData("dnxcore50", "portable-net40+win8+dnxcore50", true)]
        [InlineData("dnxcore50", "portable-net45+win8", true)]
        [InlineData("dnxcore50", "portable-net451+win81", true)]
        [InlineData("dnxcore50", "portable-net40+sl5+win8", false)]
        [InlineData("dnxcore50", "portable-net45+win8", true)]
        [InlineData("dnxcore50", "portable-net451+win81", true)]
        [InlineData("dnxcore50", "portable-net40+sl5+win8", false)]

        // dotnet
        [InlineData("dotnet", "dotnet", true)]
        [InlineData("dotnet5.1", "dotnet", true)]

        // dnxcore50 -> dotnet
        [InlineData("dnxcore50", "dotnet5.4", false)]
        [InlineData("dnxcore50", "dotnet5.3", true)]
        [InlineData("dnxcore50", "dotnet5.2", true)]
        [InlineData("dnxcore50", "dotnet5.1", true)]

        // net -> dotnet
        [InlineData("net461", "dotnet5.4", true)]
        [InlineData("net461", "dotnet5.3", true)]
        [InlineData("net461", "dotnet5.2", true)]
        [InlineData("net461", "dotnet5.1", true)]
        [InlineData("net461", "dotnet", true)]

        [InlineData("net46", "dotnet5.4", false)]
        [InlineData("net46", "dotnet5.3", true)]
        [InlineData("net46", "dotnet5.2", true)]
        [InlineData("net46", "dotnet5.1", true)]
        [InlineData("net46", "dotnet", true)]

        [InlineData("net452", "dotnet5.4", false)]
        [InlineData("net452", "dotnet5.3", false)]
        [InlineData("net452", "dotnet5.2", true)]
        [InlineData("net452", "dotnet5.1", true)]
        [InlineData("net452", "dotnet", true)]

        [InlineData("net451", "dotnet5.4", false)]
        [InlineData("net451", "dotnet5.3", false)]
        [InlineData("net451", "dotnet5.2", true)]
        [InlineData("net451", "dotnet5.1", true)]
        [InlineData("net451", "dotnet", true)]

        [InlineData("net45", "dotnet5.4", false)]
        [InlineData("net45", "dotnet5.3", false)]
        [InlineData("net45", "dotnet5.2", false)]
        [InlineData("net45", "dotnet5.1", true)]
        [InlineData("net45", "dotnet", true)]

        // dnx -> dotnet
        [InlineData("dnx461", "dotnet5.4", true)]
        [InlineData("dnx461", "dotnet5.3", true)]
        [InlineData("dnx461", "dotnet5.2", true)]
        [InlineData("dnx461", "dotnet5.1", true)]
        [InlineData("dnx461", "dotnet", true)]

        [InlineData("dnx46", "dotnet5.4", false)]
        [InlineData("dnx46", "dotnet5.3", true)]
        [InlineData("dnx46", "dotnet5.2", true)]
        [InlineData("dnx46", "dotnet5.1", true)]
        [InlineData("dnx46", "dotnet", true)]

        [InlineData("dnx452", "dotnet5.4", false)]
        [InlineData("dnx452", "dotnet5.3", false)]
        [InlineData("dnx452", "dotnet5.2", true)]
        [InlineData("dnx452", "dotnet5.1", true)]
        [InlineData("dnx452", "dotnet", true)]

        [InlineData("dnx451", "dotnet5.4", false)]
        [InlineData("dnx451", "dotnet5.3", false)]
        [InlineData("dnx451", "dotnet5.2", true)]
        [InlineData("dnx451", "dotnet5.1", true)]
        [InlineData("dnx451", "dotnet", true)]

        [InlineData("dnx45", "dotnet5.4", false)]
        [InlineData("dnx45", "dotnet5.3", false)]
        [InlineData("dnx45", "dotnet5.2", false)]
        [InlineData("dnx45", "dotnet5.1", true)]
        [InlineData("dnx45", "dotnet", true)]

        // uap10 -> netcore50 -> win81 -> wpa81 -> dotnet
        [InlineData("uap10.0", "netcore50", true)]
        [InlineData("uap10.0", "win81", true)]
        [InlineData("uap10.0", "wpa81", true)]
        [InlineData("uap10.0", "dotnet5.4", false)]
        [InlineData("uap10.0", "dotnet5.3", true)]
        [InlineData("uap10.0", "dotnet5.2", true)]
        [InlineData("uap10.0", "dotnet5.1", true)]
        [InlineData("netcore50", "win81", true)]
        [InlineData("netcore50", "wpa81", true)]
        [InlineData("netcore50", "dotnet5.4", false)]
        [InlineData("netcore50", "dotnet5.3", true)]
        [InlineData("netcore50", "dotnet5.2", true)]
        [InlineData("netcore50", "dotnet5.1", true)]

        // wpa81/win81 -> dotnet
        [InlineData("wpa81", "dotnet5.4", false)]
        [InlineData("wpa81", "dotnet5.3", false)]
        [InlineData("wpa81", "dotnet5.2", true)]
        [InlineData("wpa81", "dotnet5.1", true)]
        [InlineData("win81", "dotnet5.4", false)]
        [InlineData("win81", "dotnet5.3", false)]
        [InlineData("win81", "dotnet5.2", true)]
        [InlineData("win81", "dotnet5.1", true)]

        // wp8/win8 -> dotnet
        [InlineData("wp8", "dotnet5.4", false)]
        [InlineData("wp8", "dotnet5.3", false)]
        [InlineData("wp8", "dotnet5.2", false)]
        [InlineData("wp8", "dotnet5.1", true)]
        [InlineData("win8", "dotnet5.4", false)]
        [InlineData("win8", "dotnet5.3", false)]
        [InlineData("win8", "dotnet5.2", false)]
        [InlineData("win8", "dotnet5.1", true)]

        // Older things don't support dotnet at all
        [InlineData("sl4", "dotnet", false)]
        [InlineData("sl3", "dotnet", false)]
        [InlineData("sl2", "dotnet", false)]
        [InlineData("net40", "dotnet", false)]
        [InlineData("net35", "dotnet", false)]
        [InlineData("net20", "dotnet", false)]
        [InlineData("net20", "dotnet", false)]

        // dotnet doesn't support the things that support it
        [InlineData("dotnet5.1", "net45", false)]
        [InlineData("dotnet5.2", "net45", false)]
        [InlineData("dotnet5.2", "net451", false)]
        [InlineData("dotnet5.2", "net452", false)]
        [InlineData("dotnet5.1", "net46", false)]
        [InlineData("dotnet5.2", "net46", false)]
        [InlineData("dotnet5.3", "net46", false)]
        [InlineData("dotnet5.1", "net461", false)]
        [InlineData("dotnet5.2", "net461", false)]
        [InlineData("dotnet5.3", "net461", false)]
        [InlineData("dotnet5.4", "net461", false)]
        [InlineData("dotnet5.1", "dnxcore50", false)]
        [InlineData("dotnet5.2", "dnxcore50", false)]
        [InlineData("dotnet5.3", "dnxcore50", false)]
        [InlineData("dotnet5.4", "dnxcore50", false)]

        // Old-world Portable doesn't support dotnet and vice-versa
        [InlineData("dotnet", "portable-net40+sl5+win8", false)]
        [InlineData("portable-net40+sl5+win8", "dotnet", false)]
        [InlineData("portable-net45+win8", "dotnet", false)]
        [InlineData("portable-net451+win81", "dotnet", false)]
        [InlineData("portable-net451+win8+core50", "dotnet", false)]
        [InlineData("portable-net451+win8+dnxcore50", "dotnet", false)]
        [InlineData("portable-net451+win8+aspnetcore50", "dotnet", false)]
        public void FrameworksAreCompatible(string project, string package, bool compatible)
        {
            var frameworkName1 = VersionUtility.ParseFrameworkName(project);
            var frameworkName2 = VersionUtility.ParseFrameworkName(package);

            var result = VersionUtility.IsCompatible(frameworkName1, frameworkName2);

            Assert.Equal(compatible, result);
        }

        [Theory]
        [InlineData("dotnet", ".NETPlatform", "5.0")]
        [InlineData("dotnet10", ".NETPlatform", "1.0")]
        [InlineData("dotnet50", ".NETPlatform", "5.0")]
        [InlineData("dotnet5.1", ".NETPlatform", "5.1")]
        [InlineData("dotnet60", ".NETPlatform", "6.0")]
        public void CanParseShortFrameworkNames(string shortName, string longName, string version)
        {
            var fx = VersionUtility.ParseFrameworkName(shortName);
            Assert.Equal(new FrameworkName(longName, Version.Parse(version)), fx);
        }

        [Theory]
        [InlineData(".NETPlatform", "5.0", "dotnet")]
        [InlineData(".NETPlatform", "5.1", "dotnet5.1")]
        [InlineData(".NETPlatform", "5.2", "dotnet5.2")]
        [InlineData(".NETPlatform", "5.3", "dotnet5.3")]
        [InlineData("UAP", "10.0", "UAP10.0")]
        public void ShortFrameworkNamesAreCorrect(string longName, string version, string shortName)
        {
            var fx = new FrameworkName(longName, Version.Parse(version));
            Assert.Equal(shortName, VersionUtility.GetShortFrameworkName(fx));
        }

        [Theory]
        [InlineData("dnx46", "dotnet,dnx46", "dnx46")]
        // Profile restrictions should be honored (yes, I know net46 client doesn't exist, just for testing :))
        [InlineData("net46-client", "net46,net45-client,net40", "net45-client")]
        public void GetNearestPicksMostCompatibleItem(string input, string frameworks, string expected)
        {
            TestGetNearestPicksMostCompatibleItem(input, frameworks, expected);
        }

        [ConditionalTheory]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        // Portable frameworks should use the old matching system based on scoring to ensure the best PCL is chosen
        [InlineData(".NETPortable,Version=v4.0,Profile=Profile6", "portable-net40+sl4+win8,portable-net40+win8", "portable-net40+win8")]
        public void GetNearestPicksMostCompatibleItemOnNoneMono(string input, string frameworks, string expected)
        {
            TestGetNearestPicksMostCompatibleItem(input, frameworks, expected);
        }

        private void TestGetNearestPicksMostCompatibleItem(string input, string frameworks, string expected)
        {
            var inputFx = FrameworkNameHelper.ParseFrameworkName(input);
            var fxs = frameworks.Split(',').Select(VersionUtility.ParseFrameworkName).ToArray();
            var expectedFx = VersionUtility.ParseFrameworkName(expected);

            var actual = VersionUtility.GetNearest(inputFx, fxs);
            Assert.Equal(expectedFx, actual);
        }
    }
}
