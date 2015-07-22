// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests.NuGet
{
    public class SemanticVersionTests
    {
        public static IEnumerable<object[]> NormalizedVersionStringTestData
        {
            get
            {
                yield return new object[] { new SemanticVersion(1, 0, 0, 0), "1.0.0" };
                yield return new object[] { new SemanticVersion(1, 5, 0, 0), "1.5.0" };
                yield return new object[] { new SemanticVersion(1, 5, 1, 0), "1.5.1" };
                yield return new object[] { new SemanticVersion(1, 5, 1, 1), "1.5.1.1" };
                yield return new object[] { new SemanticVersion("1.0"), "1.0.0" };
                yield return new object[] { new SemanticVersion("1.0.0"), "1.0.0" };
                yield return new object[] { new SemanticVersion("1.0.0.0"), "1.0.0" };
                yield return new object[] { new SemanticVersion("1.0.0.2"), "1.0.0.2" };
                yield return new object[] { new SemanticVersion("1.0.0.2-beta"), "1.0.0.2-beta" };
                yield return new object[] { new SemanticVersion("1.0.0.0-beta"), "1.0.0-beta" };
                yield return new object[] { new SemanticVersion("1.0.0-beta"), "1.0.0-beta" };
            }
        }

        [Theory]
        [MemberData(nameof(NormalizedVersionStringTestData))]
        public void TestGetNormalizeVersionString(SemanticVersion version, string expectation)
        {
            Assert.Equal(expectation, version.GetNormalizedVersionString());
        }
    }
}
