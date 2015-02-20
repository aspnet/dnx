using NuGet.Frameworks;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageReaderTests
    {
        [Fact]
        public void PackageReader_NestedReferenceItems()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLibSubFolderPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetReferenceItems().ToArray();

                Assert.Equal(1, groups.Count());

                Assert.Equal(NuGetFramework.Parse("net40"), groups[0].TargetFramework);
                Assert.Equal(2, groups[0].Items.Count());
                Assert.Equal("lib/net40/test40.dll", groups[0].Items.ToArray()[0]);
                Assert.Equal("lib/net40/x86/testx86.dll", groups[0].Items.ToArray()[1]);
            }
        }

        [Fact]
        public void PackageReader_MinClientVersion()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackageMinClient());

            using (PackageReader reader = new PackageReader(zip))
            {
                var version = reader.GetMinClientVersion();

                Assert.Equal("3.0.5-beta", version.ToNormalizedString());
            }
        }

        [Fact]
        public void PackageReader_ContentWithMixedFrameworks()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackageMixed());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetContentItems().ToArray();

                Assert.Equal(3, groups.Count());
            }
        }

        [Fact]
        public void PackageReader_ContentWithFrameworks()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackageWithFrameworks());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetContentItems().ToArray();

                Assert.Equal(3, groups.Count());
            }
        }

        [Fact]
        public void PackageReader_ContentNoFrameworks()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetContentItems().ToArray();

                Assert.Equal(1, groups.Count());

                Assert.Equal(NuGetFramework.AnyFramework, groups.Single().TargetFramework);

                Assert.Equal(3, groups.Single().Items.Count());
            }
        }

        // get reference items without any nuspec entries
        [Fact]
        public void PackageReader_NoReferences()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetReferenceItems().ToArray();

                Assert.Equal(3, groups.Count());

                Assert.Equal(4, groups.SelectMany(e => e.Items).Count());
            }
        }

        // normal reference group filtering
        [Fact]
        public void PackageReader_ReferencesWithGroups()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackageWithReferenceGroups());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetReferenceItems().ToArray();

                Assert.Equal(2, groups.Count());

                Assert.Equal(NuGetFramework.AnyFramework, groups[0].TargetFramework);
                Assert.Equal(1, groups[0].Items.Count());
                Assert.Equal("lib/test.dll", groups[0].Items.Single());

                Assert.Equal(NuGetFramework.Parse("net45"), groups[1].TargetFramework);
                Assert.Equal(1, groups[1].Items.Count());
                Assert.Equal("lib/net45/test45.dll", groups[1].Items.Single());
            }
        }

        // v1.5 reference flat list applied to a 2.5+ nupkg with frameworks
        [Fact]
        public void PackageReader_ReferencesWithoutGroups()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackageWithPre25References());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetReferenceItems().ToArray();

                Assert.Equal(3, groups.Count());

                Assert.Equal(NuGetFramework.AnyFramework, groups[0].TargetFramework);
                Assert.Equal(1, groups[0].Items.Count());
                Assert.Equal("lib/test.dll", groups[0].Items.Single());

                Assert.Equal(NuGetFramework.Parse("net40"), groups[1].TargetFramework);
                Assert.Equal(1, groups[1].Items.Count());
                Assert.Equal("lib/net40/test.dll", groups[1].Items.Single());

                Assert.Equal(NuGetFramework.Parse("net451"), groups[2].TargetFramework);
                Assert.Equal(1, groups[1].Items.Count());
                Assert.Equal("lib/net451/test.dll", groups[2].Items.Single());
            }
        }

        [Fact]
        public void PackageReader_SupportedFrameworks()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                string[] frameworks = reader.GetSupportedFrameworks().Select(f => f.DotNetFrameworkName).ToArray();

                Assert.Equal("Any, Version=v0.0", frameworks[0]);
                Assert.Equal(".NETFramework, Version=v4.0", frameworks[1]);
                Assert.Equal(".NETFramework, Version=v4.5", frameworks[2]);
                Assert.Equal(frameworks.Length, 3);
            }
        }

        [Fact]
        public void PackageReader_AgnosticFramework()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                string[] frameworks = reader.GetSupportedFrameworks().Select(f => f.DotNetFrameworkName).ToArray();

                Assert.Equal("Agnostic, Version=v0.0", frameworks[0]);
                Assert.Equal(frameworks.Length, 1);
            }
        }
    }
}
