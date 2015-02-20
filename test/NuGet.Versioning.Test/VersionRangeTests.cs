using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Versioning.Test
{
    public class VersionRangeTests
    {
        [Fact]
        public void VersionRange_Exact()
        {
            // Act 
            var versionInfo = new VersionRange(new NuGetVersion(4, 3, 0), true, new NuGetVersion(4, 3, 0), true, false);

            // Assert
            Assert.True(versionInfo.Satisfies(NuGetVersion.Parse("4.3.0")));
        }

        [Fact]
        public void ParseVersionRangePrerelease()
        {
            // Act 
            var versionInfo = VersionRange.Parse("(1.2-Alpha, 1.3-Beta)");

            // Assert
            Assert.True(versionInfo.IncludePrerelease);
        }

        [Fact]
        public void ParseVersionRangeNoPrerelease()
        {
            // Act 
            var versionInfo =  new VersionRange(minVersion: new NuGetVersion("1.2-Alpha"), includePrerelease: false);

            // Assert
            Assert.False(versionInfo.IncludePrerelease);
        }

        [Theory]
        [MemberData("VersionRangeNotInRange")]
        public void ParseVersionRangeDoesNotSatisfy(string spec, string version)
        {
            // Act
            var versionInfo = VersionRange.Parse(spec);
            var middleVersion = NuGetVersion.Parse(version);

            // Assert
            Assert.False(versionInfo.Satisfies(middleVersion));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparison.Default));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparer.Default));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparison.VersionRelease));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparer.VersionRelease));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparison.VersionReleaseMetadata));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparer.VersionReleaseMetadata));
        }

        [Theory]
        [MemberData("VersionRangeInRange")]
        public void ParseVersionRangeSatisfies(string spec, string version)
        {
            // Act
            var versionInfo = VersionRange.Parse(spec);
            var middleVersion = NuGetVersion.Parse(version);

            // Assert
            Assert.True(versionInfo.Satisfies(middleVersion));
            Assert.True(versionInfo.Satisfies(middleVersion, VersionComparison.Default));
            Assert.True(versionInfo.Satisfies(middleVersion, VersionComparer.VersionRelease));
        }

        [Theory]
        [MemberData("VersionRangeParts")]
        public void ParseVersionRangeParts(NuGetVersion min, NuGetVersion max, bool minInc, bool maxInc)
        {
            // Act
            var versionInfo = new VersionRange(min, minInc, max, maxInc);

            // Assert
            Assert.Equal(min, versionInfo.MinVersion, VersionComparer.Default);
            Assert.Equal(max, versionInfo.MaxVersion, VersionComparer.Default);
            Assert.Equal(minInc, versionInfo.IsMinInclusive);
            Assert.Equal(maxInc, versionInfo.IsMaxInclusive);
        }

        [Theory]
        [MemberData("VersionRangeParts")]
        public void ParseVersionRangeToStringReParse(NuGetVersion min, NuGetVersion max, bool minInc, bool maxInc)
        {
            // Act
            var original = new VersionRange(min, minInc, max, maxInc);
            var versionInfo = VersionRange.Parse(original.ToString());

            // Assert
            Assert.Equal(min, versionInfo.MinVersion, VersionComparer.Default);
            Assert.Equal(max, versionInfo.MaxVersion, VersionComparer.Default);
            Assert.Equal(minInc, versionInfo.IsMinInclusive);
            Assert.Equal(maxInc, versionInfo.IsMaxInclusive);
        }

        [Theory]
        [MemberData("VersionRangeStrings")]
        public void ParseVersionRangeToStringShortHand(string version)
        {
            // Act
            var versionInfo = VersionRange.Parse(version);

            // Assert
            Assert.Equal(version, versionInfo.ToString("S", new VersionRangeFormatter()));
        }

        [Theory]
        [MemberData("VersionRangeStringsNormalized")]
        public void ParseVersionRangeToString(string version, string expected)
        {
            // Act
            var versionInfo = VersionRange.Parse(version);

            // Assert
            Assert.Equal(expected, versionInfo.ToString());
        }

        [Fact]
        public void ParseVersionRangeWithNullThrows()
        {
            // Act & Assert
            ExceptionAssert.ThrowsArgNull(() => VersionRange.Parse(null), "value");
        }

        [Fact]
        public void ParseVersionRangeSimpleVersionNoBrackets()
        {
            // Act
            var versionInfo = VersionRange.Parse("1.2");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal(null, versionInfo.MaxVersion);
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeSimpleVersionNoBracketsExtraSpaces()
        {
            // Act
            var versionInfo = VersionRange.Parse("  1  .   2  ");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal(null, versionInfo.MaxVersion);
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeMaxOnlyInclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("(,1.2]");

            // Assert
            Assert.Equal(null, versionInfo.MinVersion);
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal("1.2", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeMaxOnlyExclusive()
        {
            var versionInfo = VersionRange.Parse("(,1.2)");
            Assert.Equal(null, versionInfo.MinVersion);
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal("1.2", versionInfo.MaxVersion.ToString());
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeExactVersion()
        {
            // Act
            var versionInfo = VersionRange.Parse("[1.2]");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("1.2", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeMinOnlyExclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("(1.2,)");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal(null, versionInfo.MaxVersion);
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeExclusiveExclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("(1.2,2.3)");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeExclusiveInclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("(1.2,2.3]");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeInclusiveExclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("[1.2,2.3)");
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeInclusiveInclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("[1.2,2.3]");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeInclusiveInclusiveExtraSpaces()
        {
            // Act
            var versionInfo = VersionRange.Parse("   [  1 .2   , 2  .3   ]  ");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeIntegerRanges()
        {
            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse("   [1, 2]  "));
            Assert.Equal("'   [1, 2]  ' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionRangeNegativeIntegerRanges()
        {
            // Act
            VersionRange versionInfo;
            bool parsed = VersionRange.TryParse("   [-1, 2]  ", out versionInfo);

            Assert.False(parsed);
            Assert.Null(versionInfo);
        }

        [Fact]
        public void ParseVersionThrowsIfExclusiveMinAndMaxVersionRangeContainsNoValues()
        {
            // Arrange
            var versionString = "(,)";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'(,)' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfInclusiveMinAndMaxVersionRangeContainsNoValues()
        {
            // Arrange
            var versionString = "[,]";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'[,]' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfInclusiveMinAndExclusiveMaxVersionRangeContainsNoValues()
        {
            // Arrange
            var versionString = "[,)";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'[,)' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfExclusiveMinAndInclusiveMaxVersionRangeContainsNoValues()
        {
            // Arrange
            var versionString = "(,]";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'(,]' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfVersionRangeIsMissingVersionComponent()
        {
            // Arrange
            var versionString = "(,1.3..2]";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'(,1.3..2]' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfVersionRangeContainsMoreThen4VersionComponents()
        {
            // Arrange
            var versionString = "(1.2.3.4.5,1.2]";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'(1.2.3.4.5,1.2]' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionToNormalizedVersion()
        {
            // Arrange
            var versionString = "(1.0,1.2]";

            // Assert
            Assert.Equal("(1.0.0, 1.2.0]", VersionRange.Parse(versionString).ToString());
        }

        [Theory]
        [MemberData("VersionRangeStrings")]
        public void StringFormatNullProvider(string range)
        {
            // Arrange
            var versionRange = VersionRange.Parse(range);
            string actual = String.Format("{0}", versionRange);
            string expected = versionRange.ToString();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData("VersionRangeStrings")]
        public void StringFormatNullProvider2(string range)
        {
            // Arrange
            var versionRange = VersionRange.Parse(range);
            string actual = String.Format(CultureInfo.InvariantCulture, "{0}", versionRange);
            string expected = versionRange.ToString();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData("VersionRangeData")]
        public void ParseVersionParsesTokensVersionsCorrectly(string versionString, VersionRange versionRange)
        {
            // Act
            var actual = VersionRange.Parse(versionString);

            // Assert
            Assert.Equal(versionRange.IsMinInclusive, actual.IsMinInclusive);
            Assert.Equal(versionRange.IsMaxInclusive, actual.IsMaxInclusive);
            Assert.Equal(versionRange.MinVersion, actual.MinVersion);
            Assert.Equal(versionRange.MaxVersion, actual.MaxVersion);
        }

        public static IEnumerable<object[]> VersionRangeData
        {
            get
            {
                yield return new object[] { "(1.2.3.4, 3.2)", new VersionRange(NuGetVersion.Parse("1.2.3.4"), false, NuGetVersion.Parse("3.2"), false) };
                yield return new object[] { "(1.2.3.4, 3.2]", new VersionRange(NuGetVersion.Parse("1.2.3.4"), false, NuGetVersion.Parse("3.2"), true) };
                yield return new object[] { "[1.2, 3.2.5)", new VersionRange(NuGetVersion.Parse("1.2"), true, NuGetVersion.Parse("3.2.5"), false) };
                yield return new object[] { "[2.3.7, 3.2.4.5]", new VersionRange(NuGetVersion.Parse("2.3.7"), true, NuGetVersion.Parse("3.2.4.5"), true) };
                yield return new object[] { "(, 3.2.4.5]", new VersionRange(null, false, NuGetVersion.Parse("3.2.4.5"), true) };
                yield return new object[] { "(1.6, ]", new VersionRange(NuGetVersion.Parse("1.6"), false, null, true) };
                yield return new object[] { "(1.6)", new VersionRange(NuGetVersion.Parse("1.6"), false, NuGetVersion.Parse("1.6"), false) };
                yield return new object[] { "[2.7]", new VersionRange(NuGetVersion.Parse("2.7"), true, NuGetVersion.Parse("2.7"), true) };
            }
        }

        public static IEnumerable<object[]> VersionRangeStrings
        {
            get
            {
                yield return new object[] { "1.2.0" };
                yield return new object[] { "1.2.3" };
                yield return new object[] { "1.2.3-beta" };
                yield return new object[] { "1.2.3-beta+900" };
                yield return new object[] { "1.2.3-beta.2.4.55.X+900" };
                yield return new object[] { "1.2.3-0+900" };
                yield return new object[] { "[1.2.0]" };
                yield return new object[] { "[1.2.3]" };
                yield return new object[] { "[1.2.3-beta]" };
                yield return new object[] { "[1.2.3-beta+900]" };
                yield return new object[] { "[1.2.3-beta.2.4.55.X+900]" };
                yield return new object[] { "[1.2.3-0+900]" };
                yield return new object[] { "(, 1.2.0]" };
                yield return new object[] { "(, 1.2.3]" };
                yield return new object[] { "(, 1.2.3-beta]" };
                yield return new object[] { "(, 1.2.3-beta+900]" };
                yield return new object[] { "(, 1.2.3-beta.2.4.55.X+900]" };
                yield return new object[] { "(, 1.2.3-0+900]" };
            }
        }

        public static IEnumerable<object[]> VersionRangeStringsNormalized
        {
            get
            {
                yield return new object[] { "1.2.0", "[1.2.0, )" };
                yield return new object[] { "1.2.3-beta.2.4.55.X+900", "[1.2.3-beta.2.4.55.X+900, )" };
                yield return new object[] { "[1.2.0]", "[1.2.0, 1.2.0]" };
            }
        }

        public static IEnumerable<object[]> VersionRangeParts
        {
            get
            {
                yield return new object[] { NuGetVersion.Parse("1.0.0"), NuGetVersion.Parse("2.0.0"), true, true };
                yield return new object[] { NuGetVersion.Parse("1.0.0"), NuGetVersion.Parse("1.0.1"), false, false };
                yield return new object[] { NuGetVersion.Parse("1.0.0-beta+0"), NuGetVersion.Parse("2.0.0"), false, true };
                yield return new object[] { NuGetVersion.Parse("1.0.0-beta+0"), NuGetVersion.Parse("2.0.0+99"), false, false };
                yield return new object[] { NuGetVersion.Parse("1.0.0-beta+0"), NuGetVersion.Parse("2.0.0+99"), true, true };
                yield return new object[] { NuGetVersion.Parse("1.0.0"), NuGetVersion.Parse("2.0.0+99"), true, true };
            }
        }

        public static IEnumerable<object[]> VersionRangeInRange
        {
            get
            {
                yield return new object[] { "1.0.0", "2.0.0" };
                yield return new object[] { "[1.0.0, 2.0.0]", "2.0.0" };
                yield return new object[] { "[1.0.0, 2.0.0]", "1.0.0" };
                yield return new object[] { "[1.0.0, 2.0.0]", "2.0.0" };
                yield return new object[] { "[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0" };
                yield return new object[] { "[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0-beta+meta" };
                yield return new object[] { "[1.0.0-beta+meta, 2.0.0-beta+meta]", "2.0.0-beta" };
                yield return new object[] { "[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0+meta" };
                yield return new object[] { "(1.0.0-beta+meta, 2.0.0-beta+meta)", "1.0.0" };
                yield return new object[] { "(1.0.0-beta+meta, 2.0.0-beta+meta)", "2.0.0-alpha+meta" };
                yield return new object[] { "(1.0.0-beta+meta, 2.0.0-beta+meta)", "2.0.0-alpha" };
                yield return new object[] { "(, 2.0.0-beta+meta)", "2.0.0-alpha" };
            }
        }

        public static IEnumerable<object[]> VersionRangeNotInRange
        {
            get
            {
                yield return new object[] { "1.0.0", "0.0.0" };
                yield return new object[] { "[1.0.0, 2.0.0]", "2.0.1" };
                yield return new object[] { "[1.0.0, 2.0.0]", "0.0.0" };
                yield return new object[] { "[1.0.0, 2.0.0]", "3.0.0" };
                yield return new object[] { "[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0-alpha" };
                yield return new object[] { "[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0-alpha+meta" };
                yield return new object[] { "[1.0.0-beta+meta, 2.0.0-beta+meta]", "2.0.0-rc" };
                yield return new object[] { "[1.0.0-beta+meta, 2.0.0-beta+meta]", "2.0.0+meta" };
                yield return new object[] { "(1.0.0-beta+meta, 2.0.0-beta+meta)", "2.0.0-beta+meta" };
                yield return new object[] { "(, 2.0.0-beta+meta)", "2.0.0-beta+meta" };
            }
        }
    }
}
