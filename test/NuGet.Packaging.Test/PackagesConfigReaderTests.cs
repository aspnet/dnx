using NuGet.Packaging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackagesConfigReaderTests
    {

        [Fact]
        public void PackagesConfigReader_Basic()
        {
            var reader = new PackagesConfigReader(PackagesConf1);

            var version = reader.GetMinClientVersion();

            Assert.Equal("2.5.0", version.ToNormalizedString());

            var packageEntries = reader.GetPackages().ToArray();

            Assert.Equal(1, packageEntries.Length);
            Assert.Equal("Newtonsoft.Json", packageEntries[0].PackageIdentity.Id);
            Assert.Equal("6.0.4", packageEntries[0].PackageIdentity.Version.ToNormalizedString());
            Assert.Equal("net45", packageEntries[0].TargetFramework.GetShortFolderName());
            Assert.False(packageEntries[0].HasAllowedVersions);
            Assert.False(packageEntries[0].IsDevelopmentDependency);
            Assert.True(packageEntries[0].IsUserInstalled);
            Assert.False(packageEntries[0].RequireReinstallation);
            Assert.Null(packageEntries[0].AllowedVersions);
        }


        [Fact]
        public void PackagesConfigReader_Basic2()
        {
            var reader = new PackagesConfigReader(PackagesConf2);

            var version = reader.GetMinClientVersion();

            Assert.Equal("2.5.0", version.ToNormalizedString());

            var packageEntries = reader.GetPackages().ToArray();

            Assert.Equal(1, packageEntries.Length);
            Assert.Equal("Newtonsoft.Json", packageEntries[0].PackageIdentity.Id);
            Assert.Equal("6.0.4", packageEntries[0].PackageIdentity.Version.ToNormalizedString());
            Assert.Equal("net45", packageEntries[0].TargetFramework.GetShortFolderName());
            Assert.True(packageEntries[0].HasAllowedVersions);
            Assert.True(packageEntries[0].IsDevelopmentDependency);
            Assert.True(packageEntries[0].IsUserInstalled);
            Assert.True(packageEntries[0].RequireReinstallation);
            Assert.Equal("[6.0.0, )", packageEntries[0].AllowedVersions.ToString());
        }

        [Fact]
        public void PackagesConfigReader_Basic3()
        {
            var reader = new PackagesConfigReader(PackagesConf3);

            var version = reader.GetMinClientVersion();

            Assert.Equal("3.0.0", version.ToNormalizedString());

            var packageEntries = reader.GetPackages().ToArray();

            Assert.Equal(2, packageEntries.Length);
            Assert.Equal("Newtonsoft.Json", packageEntries[0].PackageIdentity.Id);
            Assert.Equal("TestPackage", packageEntries[1].PackageIdentity.Id);
        }

        private static XDocument PackagesConf1 
        {
            get
            {
                return XDocument.Parse(@"<?xml version=""1.0"" encoding=""utf-8""?>
                                <packages>
                                    <package id=""Newtonsoft.Json"" version=""6.0.4"" targetFramework=""net45"" />
                                </packages>");
            }
        }

        private static XDocument PackagesConf2
        {
            get
            {
                return XDocument.Parse(@"<?xml version=""1.0"" encoding=""utf-8""?>
                                <packages>
                                    <package id=""Newtonsoft.Json"" version=""6.0.4"" targetFramework=""net45"" allowedVersions=""6.0.0"" developmentDependency=""true"" requireReinstallation=""true"" userInstalled=""true"" />
                                </packages>");
            }
        }

        private static XDocument PackagesConf3
        {
            get
            {
                return XDocument.Parse(@"<?xml version=""1.0"" encoding=""utf-8""?>
                                <packages minClientVersion=""3.0.0"">
                                    <package id=""Newtonsoft.Json"" version=""6.0.4"" targetFramework=""net45"" />
                                    <package id=""TestPackage"" version=""1.0.0"" targetFramework=""net4"" />
                                </packages>");
            }
        }

        [Fact]
        public void PackagesConfigReader_BadMinClientVersion()
        {
            var reader = new PackagesConfigReader(BadPackagesConf1);

            Assert.Throws<PackagesConfigReaderException>(() => reader.GetMinClientVersion());
        }

        [Fact]
        public void PackagesConfigReader_BadId()
        {
            var reader = new PackagesConfigReader(BadPackagesConf1);

            Assert.Throws<PackagesConfigReaderException>(() => reader.GetPackages());
        }

        private static XDocument BadPackagesConf1
        {
            get
            {
                return XDocument.Parse(@"<?xml version=""1.0"" encoding=""utf-8""?>
                                <packages minClientVersion=""abc"">
                                    <package version=""6.0.4"" targetFramework=""net45"" />
                                    <package id=""TestPackage"" version=""1.0.0"" targetFramework=""net4"" />
                                </packages>");
            }
        }
    }
}
