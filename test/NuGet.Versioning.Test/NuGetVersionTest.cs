using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Versioning.Test
{
    public class NuGetVersionTest
    {
        [Fact]
        public void NuGetVersionConstructors()
        {
            // Arrange
            HashSet<SimpleVersion> versions = new HashSet<SimpleVersion>(VersionComparer.Default);

            // act
            versions.Add(new NuGetVersion("4.3.0"));
            versions.Add(new NuGetVersion(NuGetVersion.Parse("4.3.0")));
            versions.Add(new NuGetVersion(new Version(4, 3, 0)));
            versions.Add(new NuGetVersion(new Version(4, 3, 0), string.Empty, string.Empty));
            versions.Add(new NuGetVersion(4, 3, 0));
            versions.Add(new NuGetVersion(4, 3, 0, string.Empty));
            versions.Add(new NuGetVersion(4, 3, 0, null));
            versions.Add(new NuGetVersion(4, 3, 0, 0));
            versions.Add(new NuGetVersion(new Version(4, 3, 0), new string[0], string.Empty, "4.3"));

            versions.Add(new SemanticVersion(4, 3, 0));
            versions.Add(new SemanticVersion(4, 3, 0, string.Empty));
            versions.Add(new SemanticVersion(4, 3, 0, null));

            // Assert
            Assert.Equal<int>(1, versions.Count);
        }

        [Theory]
        [InlineData("1.0.0")]
        [InlineData("0.0.1")]
        [InlineData("1.2.3")]
        [InlineData("1.2.3-alpha")]
        [InlineData("1.2.3-X.y.3+Meta-2")]
        [InlineData("1.2.3-X.yZ.3.234.243.3242342+METADATA")]
        [InlineData("1.2.3-X.y3+0")]
        [InlineData("1.2.3-X+0")]
        [InlineData("1.2.3+0")]
        [InlineData("1.2.3-0")]
        public void NuGetVersionParseStrict(string versionString)
        {
            // Arrange
            NuGetVersion semVer = null;
            NuGetVersion.TryParseStrict(versionString, out semVer);

            // Assert
            Assert.Equal<string>(versionString, semVer.ToNormalizedString());
            Assert.Equal<string>(versionString, semVer.ToString());
        }

        [Theory]
        [MemberData("ConstructorData")]
        public void StringConstructorParsesValuesCorrectly(string version, Version versionValue, string specialValue)
        {
            // Act
            NuGetVersion semanticVersion = NuGetVersion.Parse(version);

            // Assert
            Assert.Equal(versionValue, semanticVersion.Version);
            Assert.Equal(specialValue, semanticVersion.Release);
            Assert.Equal(version, semanticVersion.ToString());
        }

        public static IEnumerable<object[]> ConstructorData
        {
            get
            {
                yield return new object[] { "1.0.0", new Version("1.0.0.0"), "" };
                yield return new object[] { "2.3-alpha", new Version("2.3.0.0"), "alpha" };
                yield return new object[] { "3.4.0.3-RC-3", new Version("3.4.0.3"), "RC-3" };
                yield return new object[] { "1.0.0-beta.x.y.5.79.0+aa", new Version("1.0.0.0"), "beta.x.y.5.79.0" };
                yield return new object[] { "1.0.0-beta.x.y.5.79.0+AA", new Version("1.0.0.0"), "beta.x.y.5.79.0" };
            }
        }

        [Fact]
        public void ParseThrowsIfStringIsNullOrEmpty()
        {
            ExceptionAssert.ThrowsArgNullOrEmpty(() => NuGetVersion.Parse(null), "value");
            ExceptionAssert.ThrowsArgNullOrEmpty(() => NuGetVersion.Parse(String.Empty), "value");
        }

        [Theory]
        [InlineData("1")]
        [InlineData("1beta")]
        [InlineData("1.2Av^c")]
        [InlineData("1.2..")]
        [InlineData("1.2.3.4.5")]
        [InlineData("1.2.3.Beta")]
        [InlineData("1.2.3.4This version is full of awesomeness!!")]
        [InlineData("So.is.this")]
        [InlineData("1.34.2Alpha")]
        [InlineData("1.34.2Release Candidate")]
        [InlineData("1.4.7-")]
        public void ParseThrowsIfStringIsNotAValidSemVer(string versionString)
        {
            ExceptionAssert.ThrowsArgumentException(() => NuGetVersion.Parse(versionString),
                "value",
                String.Format(CultureInfo.InvariantCulture, "'{0}' is not a valid version string.", versionString));
        }

        public static IEnumerable<object[]> LegacyVersionData
        {
            get
            {
                yield return new object[] { "1.022", new NuGetVersion(new Version("1.22.0.0"), "") };
                yield return new object[] { "23.2.3", new NuGetVersion(new Version("23.2.3.0"), "") };
                yield return new object[] { "1.3.42.10133", new NuGetVersion(new Version("1.3.42.10133"), "") };
            }
        }

        [Theory]
        [MemberData("LegacyVersionData")]
        public void ParseReadsLegacyStyleVersionNumbers(string versionString, NuGetVersion expected)
        {
            // Act
            var actual = NuGetVersion.Parse(versionString);

            // Assert
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.Release, actual.Release);
        }

        public static IEnumerable<object[]> SemVerData
        {
            get
            {
                yield return new object[] { "1.022-Beta", new NuGetVersion(new Version("1.22.0.0"), "Beta") };
                yield return new object[] { "23.2.3-Alpha", new NuGetVersion(new Version("23.2.3.0"), "Alpha") };
                yield return new object[] { "1.3.42.10133-PreRelease", new NuGetVersion(new Version("1.3.42.10133"), "PreRelease") };
                yield return new object[] { "1.3.42.200930-RC-2", new NuGetVersion(new Version("1.3.42.200930"), "RC-2") };
            }
        }

        [Theory]
        [MemberData("SemVerData")]
        public void ParseReadsSemverAndHybridSemverVersionNumbers(string versionString, NuGetVersion expected)
        {
            // Act
            var actual = NuGetVersion.Parse(versionString);

            // Assert
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.Release, actual.Release);
        }

        public static IEnumerable<object[]> SemVerWithWhiteSpace
        {
            get
            {
                yield return new object[] { "  1.022-Beta", new NuGetVersion(new Version("1.22.0.0"), "Beta") };
                yield return new object[] { "23.2.3-Alpha  ", new NuGetVersion(new Version("23.2.3.0"), "Alpha") };
                yield return new object[] { "    1.3.42.10133-PreRelease  ", new NuGetVersion(new Version("1.3.42.10133"), "PreRelease") };
            }
        }

        [Theory]
        [MemberData("SemVerWithWhiteSpace")]
        public void ParseIgnoresLeadingAndTrailingWhitespace(string versionString, NuGetVersion expected)
        {
            // Act
            var actual = NuGetVersion.Parse(versionString);

            // Assert
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.Release, actual.Release);
        }

        [Theory]
        [InlineData("1.0", "1.0.1")]
        [InlineData("1.23", "1.231")]
        [InlineData("1.4.5.6", "1.45.6")]
        [InlineData("1.4.5.6", "1.4.5.60")]
        [InlineData("1.01", "1.10")]
        [InlineData("1.01-alpha", "1.10-beta")]
        [InlineData("1.01.0-RC-1", "1.10.0-rc-2")]
        [InlineData("1.01-RC-1", "1.01")]
        [InlineData("1.01", "1.2-preview")]
        public void SemVerLessThanAndGreaterThanOperatorsWorks(string versionA, string versionB)
        {
            // Arrange
            var itemA = NuGetVersion.Parse(versionA);
            var itemB = NuGetVersion.Parse(versionB);
            object objectB = itemB;

            // Act and Assert
            Assert.True(itemA < itemB);
            Assert.True(itemA <= itemB);
            Assert.True(itemB > itemA);
            Assert.True(itemB >= itemA);
            Assert.False(itemA.Equals(itemB));
            Assert.False(itemA.Equals(objectB));

        }

        [Theory]
        [InlineData(new object[] { 1 })]
        [InlineData(new object[] { "1.0.0" })]
        [InlineData(new object[] { new object[0] })]
        public void EqualsReturnsFalseIfComparingANonSemVerType(object other)
        {
            // Arrange
            var semVer = NuGetVersion.Parse("1.0.0");

            // Act and Assert
            Assert.False(semVer.Equals(other));
        }

        [Theory]
        [InlineData("1.0", "1.0.0.0")]
        [InlineData("1.23.01", "1.23.1")]
        [InlineData("1.45.6", "1.45.6.0")]
        [InlineData("1.45.6-Alpha", "1.45.6-Alpha")]
        [InlineData("1.6.2-BeTa", "1.6.02-beta")]
        [InlineData("22.3.07     ", "22.3.07")]
        public void SemVerEqualsOperatorWorks(string versionA, string versionB)
        {
            // Arrange
            var itemA = NuGetVersion.Parse(versionA);
            var itemB = NuGetVersion.Parse(versionB);
            object objectB = itemB;

            // Act and Assert
            Assert.True(itemA == itemB);
            Assert.True(itemA.Equals(itemB));
            Assert.True(itemA.Equals(objectB));
            Assert.True(itemA <= itemB);
            Assert.True(itemB == itemA);
            Assert.True(itemB >= itemA);
        }

        [Fact]
        public void SemVerEqualityComparisonsWorkForNullValues()
        {
            // Arrange
            NuGetVersion itemA = null;
            NuGetVersion itemB = null;

            // Act and Assert
            Assert.True(itemA == itemB);
            Assert.True(itemB == itemA);
            Assert.True(itemA <= itemB);
            Assert.True(itemB <= itemA);
            Assert.True(itemA >= itemB);
            Assert.True(itemB >= itemA);
        }

        [Theory]
        [InlineData("1.0")]
        [InlineData("1.0.0")]
        [InlineData("1.0.0.0")]
        [InlineData("1.0-alpha")]
        [InlineData("1.0.0-b")]
        [InlineData("3.0.1.2")]
        [InlineData("2.1.4.3-pre-1")]
        public void ToStringReturnsOriginalValue(string version)
        {
            // Act
            NuGetVersion semVer = NuGetVersion.Parse(version);

            // Assert
            Assert.Equal(version, semVer.ToString());
        }

        public static IEnumerable<object[]> ToStringFromVersionData
        {
            get
            {
                yield return new object[] { new Version("1.0"), null, "1.0" };
                yield return new object[] { new Version("1.0.3.120"), String.Empty, "1.0.3.120" };
                yield return new object[] { new Version("1.0.3.120"), "alpha", "1.0.3.120-alpha" };
                yield return new object[] { new Version("1.0.3.120"), "rc-2", "1.0.3.120-rc-2" };
            }
        }

        [Theory]
        [MemberData("ToStringFromVersionData")]
        public void ToStringConstructedFromVersionAndSpecialVersionConstructor(Version version, string specialVersion, string expected)
        {
            // Act
            NuGetVersion semVer = new NuGetVersion(version, specialVersion);

            // Assert
            Assert.Equal(expected, semVer.ToString());
        }

        [Theory]
        [MemberData("ToStringFromVersionData")]
        public void ToStringFromStringFormat(Version version, string specialVersion, string expected)
        {
            // Act
            NuGetVersion semVer = new NuGetVersion(version, specialVersion);

            // Assert
            Assert.Equal(expected, String.Format("{0}", semVer));
        }



        [Fact]
        public void TryParseStrictParsesStrictVersion()
        {
            // Arrange
            var versionString = "1.3.2-CTP-2-Refresh-Alpha";

            // Act
            NuGetVersion version;
            bool result = NuGetVersion.TryParseStrict(versionString, out version);

            // Assert
            Assert.True(result);
            Assert.Equal(new Version("1.3.2.0"), version.Version);
            Assert.Equal("CTP-2-Refresh-Alpha", version.Release);
        }

    }
}
