using NuGet.Packaging;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageFolderReaderTests
    {
        // verify a zip package reader, and folder package reader handle reference items the same
        [Fact]
        public void PackageFolderReader_Basic()
        {
            FileInfo packageNupkg = TestPackages.GetLegacyTestPackage();
            var zip = new ZipArchive(packageNupkg.OpenRead());
            PackageReader zipReader = new PackageReader(zip);

            string folder = Path.Combine(packageNupkg.Directory.FullName, Guid.NewGuid().ToString());

            var zipFile = new ZipArchive(File.OpenRead(packageNupkg.FullName));

            zipFile.ExtractAll(folder);

            var folderReader = new PackageFolderReader(folder);

            Assert.Equal(zipReader.GetIdentity(), folderReader.GetIdentity(), new PackageIdentityComparer());

            Assert.Equal(zipReader.GetLibItems().Count(), folderReader.GetLibItems().Count());

            Assert.Equal(zipReader.GetReferenceItems().Count(), folderReader.GetReferenceItems().Count());

            Assert.Equal(zipReader.GetReferenceItems().First().Items.First(), folderReader.GetReferenceItems().First().Items.First());
        }
    }
}
