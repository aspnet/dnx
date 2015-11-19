// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CompilationAbstractions;
using Xunit;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    public class ResourceForCultureTest
    {
        [Theory]
        [InlineData("Program.fr-FR.resources","fr-FR")]
        [InlineData("Program.fr-12.resources", "")]
        [InlineData("Program.fr.resources", "fr")]
        [InlineData("Program.az-Latn-AZ.resources", "az-Latn-AZ")]
        [InlineData("Program.ay.resources", "")]
        [InlineData("", "")]
        [InlineData("Program.resources", "")]
        [InlineData("Program.fr--FR.resources", "")]
        [InlineData("Program.f.resources", "")]
        [InlineData("Program.resources", "")]
        [InlineData("Program.a1.resources", "")]
        [InlineData("Program.a1.test", "")]
        [InlineData("Program.html", "")]
        [InlineData("TestProject.Class.Program.resx","")]
        [InlineData("TestProject.Class.Program.fr-FR.resx", "fr-FR")]
        [InlineData("TestProject.Class-Lib.fr-FR.resources","fr-FR")]
        [InlineData("TestProject.Class-Lib.resources", "")]
        public void GetResourceCultureNameTest(string fileName, string expectedCulture)
        {
            var resourceDescriptor = new ResourceDescriptor();
            resourceDescriptor.FileName = fileName;
            var culture = ResourcesForCulture.GetResourceCultureName(resourceDescriptor);
            Assert.Equal(expectedCulture, culture);
        }
    }
}
