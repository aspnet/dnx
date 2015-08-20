// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests.NuGet.Authoring
{
    public class ManifestMetadataTests
    {
        [Fact]
        public void AuthorsIsEmptyByDefault()
        {
            var metadata = new ManifestMetadata();

            Assert.Empty(metadata.Authors);
        }

        [Fact]
        public void OWnersIsEmptyByDefault()
        {
            var metadata = new ManifestMetadata();

            Assert.Empty(metadata.Owners);
        }

        [Fact]
        public void OwnersFallbackToAuthors()
        {
            var metadata = new ManifestMetadata();
            metadata.Authors = new string[] { "A", "B" };

            Assert.Equal(new string[] { "A", "B" }, metadata.Owners);
        }
    }
}
