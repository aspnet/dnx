using System;
using System.Collections.Generic;
using System.Linq;
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

        // dotnet
        [InlineData("dotnet", "dotnet", true)]
        [InlineData("dnxcore50", "dotnet", true)]
        [InlineData("aspnetcore50", "dotnet", true)]
        [InlineData("dnx451", "dotnet", true)]
        [InlineData("dnx46", "dotnet", true)]
        [InlineData("net451", "dotnet", true)]
        [InlineData("net45", "dotnet", true)]
        [InlineData("net40", "dotnet", false)]
        [InlineData("net46", "dotnet", true)]
        [InlineData("sl20", "dotnet", false)]
        [InlineData("dotnet", "portable-net40+sl5+win8", false)]
        [InlineData("dotnet", "portable-net45+win8", true)]
        [InlineData("dotnet", "portable-net451+win81", true)]
        [InlineData("dotnet", "portable-net451+win8+core50", true)]
        [InlineData("dotnet", "portable-net451+win8+dnxcore50", true)]
        [InlineData("dotnet", "portable-net451+win8+aspnetcore50", true)]

        // Old-world Portable doesn't support dotnet
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

        [Theory]
        [InlineData("dnx46", "dotnet,dnx46", "dnx46")]
        public void GetNearestPicksMostCompatibleItem(string input, string frameworks, string expected)
        {
            var inputFx = VersionUtility.ParseFrameworkName(input);
            var fxs = frameworks.Split(',').Select(VersionUtility.ParseFrameworkName).ToArray();
            var expectedFx = VersionUtility.ParseFrameworkName(expected);

            var items = fxs.Select(fx => new PackageDependencySet(fx, Enumerable.Empty<PackageDependency>()));
            IEnumerable<PackageDependencySet> selectedItems;
            Assert.True(VersionUtility.TryGetCompatibleItems(inputFx, items, out selectedItems));
            var selectedItem = selectedItems.SingleOrDefault();
            Assert.NotNull(selectedItem);
            Assert.Equal(expectedFx, selectedItem.TargetFramework);
        }
    }
}
