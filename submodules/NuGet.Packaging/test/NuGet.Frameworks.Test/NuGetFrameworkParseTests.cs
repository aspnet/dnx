using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Test
{
    public class NuGetFrameworkParseTests
    {
        [Fact]
        public void NuGetFramework_SpecialNamesToDotNetFrameworkName()
        {
            Assert.Equal("Any, Version=v0.0", NuGetFramework.AnyFramework.DotNetFrameworkName);
            Assert.Equal("Agnostic, Version=v0.0", NuGetFramework.AgnosticFramework.DotNetFrameworkName);
            Assert.Equal("Unsupported, Version=v0.0", NuGetFramework.UnsupportedFramework.DotNetFrameworkName);
        }

        [Fact]
        public void NuGetFramework_PortableWithOptional()
        {
            NuGetFramework framework = NuGetFramework.Parse("portable-net4%2Bsl5%2Bwp8%2Bwin8%2Bwpa81%2Bmonotouch%2Bmonoandroid");

            Assert.Equal(".NETPortable, Version=v0.0, Profile=Profile328", framework.DotNetFrameworkName);
        }

        [Fact]
        public void NuGetFramework_PortableWithAny()
        {
            NuGetFramework framework = NuGetFramework.Parse("portable-win%2Bnet45%2Bwp8");

            Assert.Equal(".NETPortable, Version=v0.0, Profile=Profile78", framework.DotNetFrameworkName);
        }

        [Fact]
        public void NuGetFramework_IncludeUnknownProfile()
        {
            string actual = NuGetFramework.Parse("net45-custom").DotNetFrameworkName;

            Assert.Equal(".NETFramework, Version=v4.5, Profile=custom", actual);
        }

        [Theory]
        [InlineData(".NETPortable40-Profile1", ".NETPortable, Version=v4.0, Profile=Profile1")]
        [InlineData(".NETPortable-Profile1", ".NETPortable, Version=v0.0, Profile=Profile1")]
        [InlineData(".NETPortable-net45+win8", ".NETPortable, Version=v0.0, Profile=Profile7")]
        public void NuGetFramework_PortableMixed(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("foo45", "Unsupported, Version=v0.0")]
        [InlineData("", "Unsupported, Version=v0.0")]
        public void NuGetFramework_ParseUnknown(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(".NETFramework45", ".NETFramework, Version=v4.5")]
        [InlineData("Portable-net45+win8", ".NETPortable, Version=v0.0, Profile=Profile7")]
        [InlineData("windows8", "Windows, Version=v8.0")]
        [InlineData("windowsphone8", "WindowsPhone, Version=v8.0")]
        public void NuGetFramework_PartialFull(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(".NETFramework, Version=v4.5", ".NETFramework, Version=v4.5")]
        [InlineData("NETFramework, Version=v4.5", ".NETFramework, Version=v4.5")]
        [InlineData(".NETPortable, Version=v0.0, Profile=Profile7", ".NETPortable, Version=v0.0, Profile=Profile7")]
        [InlineData("Portable, Version=v0.0, Profile=Profile7", ".NETPortable, Version=v0.0, Profile=Profile7")]
        public void NuGetFramework_ParseFullName(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("portable-net45+win10.0", ".NETPortable, Version=v0.0, Profile=net45+win10.0")]
        [InlineData("portable-net45+win8", ".NETPortable, Version=v0.0, Profile=Profile7")]
        [InlineData("portable-win8+net45", ".NETPortable, Version=v0.0, Profile=Profile7")]
        [InlineData("portable-win8+net45+monoandroid1+monotouch1", ".NETPortable, Version=v0.0, Profile=Profile7")]
        public void NuGetFramework_Portable(string folder, string expected)
        {
            string actual = NuGetFramework.Parse(folder).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("net45-cf", "CompactFramework")]
        [InlineData("net45-CF", "CompactFramework")]
        [InlineData("net45-Full", "")]
        [InlineData("net45", "")]
        [InlineData("net45-WP71", "WindowsPhone71")]
        [InlineData("net45-WP", "WindowsPhone")]
        public void NuGetFramework_ProfileName(string folder, string expected)
        {
            string actual = NuGetFramework.Parse(folder).Profile;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("net45", ".NETFramework, Version=v4.5")]
        [InlineData("net20", ".NETFramework, Version=v2.0")]
        [InlineData("net", ".NETFramework, Version=v0.0")]
        [InlineData("net10.1.2.3", ".NETFramework, Version=v10.1.2.3")]
        [InlineData("net45-cf", ".NETFramework, Version=v4.5, Profile=CompactFramework")]
        public void NuGetFramework_Basic(string folderName, string fullName)
        {
            string output = NuGetFramework.Parse(folderName).DotNetFrameworkName;

            Assert.Equal(fullName, output);
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("foo45")]
        [InlineData("foo45-client")]
        [InlineData("45")]
        [InlineData("foo.45")]
        [InlineData("foo4.5.1.2.3")]
        [InlineData("portable-net($3747!4")]
        [InlineData("")]
        public void NuGetFramework_Unsupported(string folderName)
        {
            Assert.Equal("Unsupported, Version=v0.0", NuGetFramework.Parse(folderName).DotNetFrameworkName);
        }
    }
}
