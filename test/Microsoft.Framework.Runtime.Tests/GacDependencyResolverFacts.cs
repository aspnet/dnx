// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Testing.xunit;
using Microsoft.Framework.Runtime.Helpers;
using NuGet;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class GacDependencyResolverFacts
    {
        [ConditionalTheory]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
        [InlineData("mscorlib", "4.0.0.0", "dnx451", true)]
        [InlineData("mscorlib", "1.0.0.0", "dnx451", false)]
        [InlineData("mscorlib", "4.0.0.0", "dnxcore50", false)]
        public void GetDescriptionReturnsCorrectResults(string name, string version, string framework, bool found)
        {
            var libraryRange = new LibraryRange()
            {
                Name = name,
                VersionRange = VersionUtility.ParseVersionRange(version),
                IsGacOrFrameworkReference = true
            };

            var frameworkName = FrameworkNameHelper.ParseFrameworkName(framework);
            var resolver = new GacDependencyResolver();
            var library = resolver.GetDescription(libraryRange, frameworkName);

            if (found)
            {
                Assert.NotNull(library);
                Assert.Equal(SemanticVersion.Parse(version), library.Identity.Version);
            }
            else
            {
                Assert.Null(library);
            }
        }
    }
}