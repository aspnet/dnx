// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests.NuGet.Authoring
{
    public class ManifestTests
    {
        [Fact]
        public void ContructorThrowsWhenMetadataIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                var manifest = new Manifest(null);
            });
        }

        [Fact]
        public void FilesListIsEmptyByDefault()
        {
            var manifest = new Manifest(new ManifestMetadata());

            Assert.NotNull(manifest.Files);
            Assert.Empty(manifest.Files);
        }

        [Fact]
        public void Save()
        {
            var id = "TestId";
            var version = new SemanticVersion("0.1.0");
            var authors = new string[] { "Alice", "Bob" };

            var manifest = new Manifest(new ManifestMetadata
            {
                Id = id,
                Version = version,
                Authors = authors
            });

            using (var mem = new MemoryStream())
            {
                manifest.Save(mem);

                mem.Position = 0;
                var xdoc = XDocument.Load(mem);
                var ns = xdoc.Root.GetDefaultNamespace();

                var xElemPackage = xdoc.Root;
                Assert.Equal("package", xElemPackage.Name.LocalName);
                Assert.Equal(1, xElemPackage.Elements().Count());

                var xElemMetadata = xElemPackage.Elements().Single();
                Assert.Equal("metadata", xElemMetadata.Name.LocalName);
                Assert.Equal(5, xElemMetadata.Elements().Count());

                var xElemId = xElemMetadata.Element(ns + "id");
                Assert.NotNull(xElemId);
                Assert.Equal(id, xElemId.Value);

                var xElemVersion = xElemMetadata.Element(ns + "version");
                Assert.NotNull(xElemVersion);
                Assert.Equal(version.OriginalString, xElemVersion.Value);

                var xElemRequireLicense = xElemMetadata.Element(ns + "requireLicenseAcceptance");
                Assert.NotNull(xElemRequireLicense);
                Assert.Equal("false", xElemRequireLicense.Value);

                var xElemAuthors = xElemMetadata.Element(ns + "authors");
                Assert.NotNull(xElemAuthors);
                Assert.Equal(string.Join(",", authors), xElemAuthors.Value);

                var xElemOwners = xElemMetadata.Element(ns + "owners");
                Assert.NotNull(xElemOwners);
                Assert.Equal(string.Join(",", authors), xElemOwners.Value);
            }
        }
    }
}
