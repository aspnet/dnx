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
    public class VersionParsingTests
    {
        [Theory]
        [InlineData("1.0.0-Beta")]
        [InlineData("1.0.0-Beta.2")]
        [InlineData("1.0.0+MetaOnly")]
        [InlineData("1.0.0")]
        [InlineData("1.0.0-Beta+Meta")]
        [InlineData("1.0.0-RC.X+MetaAA")]
        [InlineData("1.0.0-RC.X.35.A.3455+Meta-A-B-C")]
        public void FullVersionParsing(string version)
        {
            // Arrange & Act
            var versions = Parse(version);

            // Assert
            versions.ForEach(v =>
            {
                Assert.Equal(version, v.ToNormalizedString());
            });
        }


        [Theory]
        [InlineData("Beta", "1.0.0-Beta")]
        [InlineData("Beta", "1.0.0-Beta+Meta")]
        [InlineData("RC.X", "1.0.0-RC.X+Meta")]
        [InlineData("RC.X.35.A.3455", "1.0.0-RC.X.35.A.3455+Meta")]
        public void SpecialVersionParsing(string expected, string version)
        {
            // Arrange & Act
            var versions = Parse(version);

            // Assert
            versions.ForEach(v =>
            {
                Assert.Equal(expected, v.Release);
            });
        }

        [Theory]
        [InlineData(new string[] { }, "1.0.0+Metadata")]
        [InlineData(new string[] { }, "1.0.0")]
        [InlineData(new string[] { "Beta" }, "1.0.0-Beta")]
        [InlineData(new string[] { "Beta" }, "1.0.0-Beta+Meta")]
        [InlineData(new string[] { "RC", "X" }, "1.0.0-RC.X+Meta")]
        [InlineData(new string[] { "RC","X","35","A","3455" }, "1.0.0-RC.X.35.A.3455+Meta")]
        public void ReleaseLabelParsing(string[] expected, string version)
        {
            // Arrange & Act
            var versions = Parse(version);

            // Assert
            versions.ForEach(v =>
            {
                Assert.Equal(expected, v.ReleaseLabels);
            });
        }

        [Theory]
        [InlineData(false, "1.0.0+Metadata")]
        [InlineData(false, "1.0.0")]
        [InlineData(true, "1.0.0-Beta")]
        [InlineData(true, "1.0.0-Beta+Meta")]
        [InlineData(true, "1.0.0-RC.X+Meta")]
        [InlineData(true, "1.0.0-RC.X.35.A.3455+Meta")]
        public void IsPrereleaseParsing(bool expected, string version)
        {
            // Arrange & Act
            var versions = Parse(version);

            // Assert
            versions.ForEach(v =>
            {
                Assert.Equal(expected, v.IsPrerelease);
            });
        }

        [Theory]
        [InlineData("", "1.0.0-Beta")]
        [InlineData("Meta", "1.0.0-Beta+Meta")]
        [InlineData("MetaAA", "1.0.0-RC.X+MetaAA")]
        [InlineData("Meta-A-B-C", "1.0.0-RC.X.35.A.3455+Meta-A-B-C")]
        public void MetadataParsing(string expected, string version)
        {
            // Arrange & Act
            var versions = Parse(version);

            // Assert
            versions.ForEach(v =>
            {
                Assert.Equal(expected, v.Metadata);
            });
        }

        [Theory]
        [InlineData(false, "1.0.0-Beta")]
        [InlineData(false, "1.0.0-Beta.2")]
        [InlineData(true, "1.0.0+MetaOnly")]
        [InlineData(false, "1.0.0")]
        [InlineData(true, "1.0.0-Beta+Meta")]
        [InlineData(true, "1.0.0-RC.X+MetaAA")]
        [InlineData(true, "1.0.0-RC.X.35.A.3455+Meta-A-B-C")]
        public void HasMetadataParsing(bool expected, string version)
        {
            // Arrange & Act
            var versions = Parse(version);

            // Assert
            versions.ForEach(v =>
            {
                Assert.Equal(expected, v.HasMetadata);
            });
        }

        [Theory]
        [InlineData(0, 0, 0, "0.0.0")]
        [InlineData(1, 0, 0, "1.0.0")]
        [InlineData(3, 5, 1, "3.5.1")]
        [InlineData(234, 234234, 1111, "234.234234.1111")]
        [InlineData(3, 5, 1, "3.5.1+Meta")]
        [InlineData(3, 5, 1, "3.5.1-x.y.z+AA")]
        public void VersionParsing(int major, int minor, int patch, string version)
        {
            // Arrange & Act
            var versions = Parse(version);

            // Assert
            versions.ForEach(v =>
            {
                Assert.Equal(major, v.Major);
                Assert.Equal(minor, v.Minor);
                Assert.Equal(patch, v.Patch);
            });
        }

        // All possible ways to parse a version from a string
        private static List<NuGetVersion> Parse(string version)
        {
            // Parse
            List<NuGetVersion> versions = new List<NuGetVersion>();
            versions.Add(NuGetVersion.Parse(version));
            versions.Add(NuGetVersion.Parse(version));

            // TryParse
            NuGetVersion semVer = null;
            NuGetVersion.TryParse(version, out semVer);
            versions.Add(semVer);

            NuGetVersion nuVer = null;
            NuGetVersion.TryParse(version, out nuVer);
            versions.Add(nuVer);

            // TryParseStrict
            nuVer = null;
            NuGetVersion.TryParseStrict(version, out nuVer);
            versions.Add(nuVer);

            // Constructors
            var normal = NuGetVersion.Parse(version);

            versions.Add(normal);
            versions.Add(new NuGetVersion(NuGetVersion.Parse(version)));

            return versions;
        }
    }
}
