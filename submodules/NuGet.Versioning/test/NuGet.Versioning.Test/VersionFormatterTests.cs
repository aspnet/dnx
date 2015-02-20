using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Versioning.Test
{
    public class VersionFormatterTests
    {
        [Theory]
        [InlineData("1.2.3.4-RC+99", "1.2.3.4-RC+99")]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("1.2.3-Pre.2", "1.2.3-Pre.2")]
        [InlineData("1.2.3+99", "1.2.3+99")]
        [InlineData("1.2-Pre", "1.2.0-Pre")]
        public void NormalizedFormatTest(string versionString, string expected)
        {
            // arrange
            VersionFormatter formatter = new VersionFormatter();
            NuGetVersion version = NuGetVersion.Parse(versionString);

            // act
            string s = String.Format(formatter, "{0:N}", version);
            string s2 = version.ToString("N", formatter);

            // assert
            Assert.Equal(expected, s);
            Assert.Equal(expected, s2);
        }

        [Theory]
        [InlineData("1.2.3.4-RC+99", "99")]
        [InlineData("1.2.3", "")]
        [InlineData("1.2.3+A2", "A2")]
        public void FormatMetadataTest(string versionString, string expected)
        {
            // arrange
            VersionFormatter formatter = new VersionFormatter();
            NuGetVersion version = NuGetVersion.Parse(versionString);

            // act
            string s = String.Format(formatter, "{0:M}", version);
            string s2 = version.ToString("M", formatter);

            // assert
            Assert.Equal(expected, s);
            Assert.Equal(expected, s2);
        }

        [Theory]
        [InlineData("1.2.3.4-RC+99", "RC")]
        [InlineData("1.2.3.4-RC.2+99", "RC.2")]
        [InlineData("1.2.3", "")]
        [InlineData("1.2.3+A2", "")]
        public void FormatReleaseTest(string versionString, string expected)
        {
            // arrange
            VersionFormatter formatter = new VersionFormatter();
            NuGetVersion version = NuGetVersion.Parse(versionString);

            // act
            string s = String.Format(formatter, "{0:R}", version);
            string s2 = version.ToString("R", formatter);

            // assert
            Assert.Equal(expected, s);
            Assert.Equal(expected, s2);
        }

        [Theory]
        [InlineData("1.2.3.4-RC+99", "1.2.3.4")]
        [InlineData("1.2.3.4-RC.2+99", "1.2.3.4")]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("1.2+A2", "1.2.0")]
        public void FormatVersionTest(string versionString, string expected)
        {
            // arrange
            VersionFormatter formatter = new VersionFormatter();
            NuGetVersion version = NuGetVersion.Parse(versionString);

            // act
            string s = String.Format(formatter, "{0:V}", version);
            string s2 = version.ToString("V", formatter);

            // assert
            Assert.Equal(expected, s);
            Assert.Equal(expected, s2);
        }

        [Theory]
        [InlineData("1.2.3.4-RC+99", "1.1.2.3.4(99)*RC: 1.2.3.4")]
        [InlineData("1.2.3.4-RC.2+99", "1.1.2.3.4(99)*RC.2: 1.2.3.4")]
        [InlineData("1.2.3", "1.1.2.3.0()*: 1.2.3")]
        [InlineData("1.2.3+A2", "1.1.2.3.0(A2)*: 1.2.3")]
        public void FormatComplexTest(string versionString, string expected)
        {
            // arrange
            VersionFormatter formatter = new VersionFormatter();
            NuGetVersion version = NuGetVersion.Parse(versionString);

            // act
            string s = String.Format(formatter, "{0:x}.{0:x}.{0:y}.{0:z}.{0:r}({0:M})*{0:R}: {0:V}", version, version, version, version, version, version, version, version);
            string s2 = version.ToString("x.x.y.z.r(M)*R: V", formatter);

            // assert
            Assert.Equal(expected, s);
            Assert.Equal(expected, s2);
        }
    }
}
