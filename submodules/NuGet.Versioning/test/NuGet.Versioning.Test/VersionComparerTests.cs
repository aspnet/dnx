using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Versioning.Test
{
    public class VersionComparerTests
    {
        [Theory]
        [InlineData("1.0.0", "1.0.0")]
        [InlineData("1.0.0-BETA", "1.0.0-beta")]
        [InlineData("1.0.0-BETA+AA", "1.0.0-beta+aa")]
        [InlineData("1.0.0-BETA+AA", "1.0.0-beta+aa")]
        [InlineData("1.0.0-BETA.X.y.5.77.0+AA", "1.0.0-beta.x.y.5.77.0+aa")]
        public void VersionComparisonDefaultEqual(string version1, string version2)
        {
            // Arrange & Act
            bool match = Equals(VersionComparer.Default, version1, version2);

            // Assert
            Assert.True(match);
        }

        [Theory]
        [InlineData("0.0.0", "1.0.0")]
        [InlineData("1.1.0", "1.0.0")]
        [InlineData("1.0.1", "1.0.0")]
        [InlineData("1.0.1", "1.0.0")]
        [InlineData("1.0.0-BETA", "1.0.0-beta2")]
        [InlineData("1.0.0+AA", "1.0.0-beta+aa")]
        [InlineData("1.0.0-BETA+AA", "1.0.0-beta")]
        [InlineData("1.0.0-BETA.X.y.5.77.0+AA", "1.0.0-beta.x.y.5.79.0+aa")]
        public void VersionComparisonDefaultNotEqual(string version1, string version2)
        {
            // Arrange & Act
            bool match = !Equals(version1, version2);

            // Assert
            Assert.True(match);
        }

        [Theory]
        [InlineData("0.0.0", "1.0.0")]
        [InlineData("1.0.0", "1.1.0")]
        [InlineData("1.0.0", "1.0.1")]
        [InlineData("1.999.9999", "2.1.1")]
        [InlineData("1.0.0-BETA", "1.0.0-beta2")]
        [InlineData("1.0.0-beta+AA", "1.0.0+aa")]
        [InlineData("1.0.0-BETA", "1.0.0-beta.1+AA")]
        [InlineData("1.0.0-BETA.X.y.5.77.0+AA", "1.0.0-beta.x.y.5.79.0+aa")]
        [InlineData("1.0.0-BETA.X.y.5.79.0+AA", "1.0.0-beta.x.y.5.790.0+abc")]
        public void VersionComparisonDefaultLess(string version1, string version2)
        {
            // Arrange & Act
            int result = Compare(VersionComparer.Default, version1, version2);

            // Assert
            Assert.True(result < 0);
        }

        
        private static int Compare(IVersionComparer comparer, string version1, string version2)
        {
            // Act
            int x = CompareOneWay(comparer, version1, version2);
            int y = CompareOneWay(comparer, version2, version1) * -1;

            // Assert
            Assert.Equal(x, y);

            return x;
        }

        
        private static int CompareOneWay(IVersionComparer comparer, string version1, string version2)
        {
            // Arrange
            NuGetVersion a = NuGetVersion.Parse(version1);
            NuGetVersion b = NuGetVersion.Parse(version2);
            SemanticVersion c = SemanticVersion.Parse(version1);
            SemanticVersion d = SemanticVersion.Parse(version2);

            // Act
            List<int> results = new List<int>();
            results.Add(comparer.Compare(a, b));
            results.Add(comparer.Compare(a, d));
            results.Add(comparer.Compare(c, b));
            results.Add(comparer.Compare(c, d));

            // Assert
            Assert.True(results.FindAll(x => x == results[0]).Count == results.Count);

            return results[0];
        } 

        private static bool Equals(IVersionComparer comparer, string version1, string version2)
        {
            return EqualsOneWay(comparer, version1, version2) && EqualsOneWay(comparer, version2, version1);
        }

        
        private static bool EqualsOneWay(IVersionComparer comparer, string version1, string version2)
        {
            // Arrange
            NuGetVersion a = NuGetVersion.Parse(version1);
            NuGetVersion b = NuGetVersion.Parse(version2);
            SemanticVersion c = NuGetVersion.Parse(version1);
            SemanticVersion d = NuGetVersion.Parse(version2);

            // Act
            bool match = Compare(comparer, version1, version2) == 0;
            match &= comparer.Equals(a, b);
            match &= comparer.Equals(a, d);
            match &= comparer.Equals(c, d);
            match &= comparer.Equals(c, b);

            return match;
        }
    }
}
