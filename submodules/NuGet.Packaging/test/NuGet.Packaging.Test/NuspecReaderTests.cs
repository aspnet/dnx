using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class NuspecReaderTests
    {
        private const string BasicNuspec = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""jQuery"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                        <group targetFramework=""wp8"">
                          <dependency id=""jQuery"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string NamespaceOnMetadataNuspec = @"<?xml version=""1.0""?>
                <package xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                    <metadata xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                    <id>packageB</id>
                    <version>1.0</version>
                    <authors>nuget</authors>
                    <owners>nuget</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>test</description>
                    </metadata>
                </package>";

        [Fact]
        public void NuspecReaderTests_NamespaceOnMetadata()
        {
            NuspecReader reader = GetReader(NamespaceOnMetadataNuspec);

            string id = reader.GetId();

            Assert.Equal("packageB", id);
        }

        [Fact]
        public void NuspecReaderTests_Id()
        {
            NuspecReader reader = GetReader(BasicNuspec);

            string id = reader.GetId();

            Assert.Equal("packageA", id);
        }

        [Fact]
        public void NuspecReaderTests_DependencyGroups()
        {
            NuspecReader reader = GetReader(BasicNuspec);

            var dependencies = reader.GetDependencyGroups().ToList();

            Assert.Equal(2, dependencies.Count);
        }

        [Fact]
        public void NuspecReaderTests_Language()
        {
            NuspecReader reader = GetReader(BasicNuspec);

            var language = reader.GetLanguage();

            Assert.Equal("en-US", language);
        }


        private static NuspecReader GetReader(string nuspec)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(nuspec)))
            {
                return new NuspecReader(stream);
            }
        }
    }
}
