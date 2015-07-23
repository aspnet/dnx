// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.PackageManager;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Tooling.Tests
{
    public class PackageSourceUtilsFacts
    {
        [Theory]
        [InlineData(@"C:\\foo\\bar", true)]
        [InlineData(@".\foo\bar", true)]
        [InlineData(@"foo\bar", true)]
        [InlineData(@"/var/NuGet/packages", true)]
        [InlineData(@"foo/bar", true)]
        [InlineData(@"\\file\share", true)]
        [InlineData(@"http://www.nuget.org", false)]
        [InlineData(@"https://www.nuget.org", false)]
        public void IsLocalFileSystem_CorrectlyIdentifiesIfStringIsLocalFileSystemPath(string path, bool isFileSystem)
        {
            Assert.Equal(isFileSystem, new PackageSource(path).IsLocalFileSystem());
        }
    }
}
