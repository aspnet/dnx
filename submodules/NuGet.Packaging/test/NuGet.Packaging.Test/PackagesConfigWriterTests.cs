using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackagesConfigWriterTests
    {

        [Fact]
        public void PackagesConfigWriter_Basic()
        {
            MemoryStream stream = new MemoryStream();

            using (PackagesConfigWriter writer = new PackagesConfigWriter(stream))
            {
                writer.WriteMinClientVersion(NuGetVersion.Parse("3.0.1"));
                
                writer.WritePackageEntry("packageB", NuGetVersion.Parse("2.0.0"), NuGetFramework.Parse("portable-net45+win8"));

                writer.WritePackageEntry("packageA", NuGetVersion.Parse("1.0.1"), NuGetFramework.Parse("net45"));
            }

            stream.Seek(0, SeekOrigin.Begin);

            var xml = XDocument.Load(stream);

            Assert.Equal("utf-8", xml.Declaration.Encoding);

            PackagesConfigReader reader = new PackagesConfigReader(xml);

            Assert.Equal("3.0.1", reader.GetMinClientVersion().ToNormalizedString());

            var packages = reader.GetPackages().ToArray();
            Assert.Equal("packageA", packages[0].PackageIdentity.Id);
            Assert.Equal("packageB", packages[1].PackageIdentity.Id);

            Assert.Equal("1.0.1", packages[0].PackageIdentity.Version.ToNormalizedString());
            Assert.Equal("2.0.0", packages[1].PackageIdentity.Version.ToNormalizedString());

            Assert.Equal("net45", packages[0].TargetFramework.GetShortFolderName());
            Assert.Equal("portable-net45+win8+monoandroid+monotouch", packages[1].TargetFramework.GetShortFolderName());
        }

        [Fact]
        public void PackagesConfigWriter_Duplicate()
        {
            MemoryStream stream = new MemoryStream();

            using (PackagesConfigWriter writer = new PackagesConfigWriter(stream))
            {
                writer.WritePackageEntry("packageA", NuGetVersion.Parse("1.0.1"), NuGetFramework.Parse("net45"));

                Assert.Throws<PackagingException>(() => writer.WritePackageEntry("packageA", NuGetVersion.Parse("2.0.1"), NuGetFramework.Parse("net4")));
            }
        }
    }
}
