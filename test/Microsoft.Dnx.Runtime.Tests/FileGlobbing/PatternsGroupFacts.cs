// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests.FileGlobbing
{
    public class PatternsGroupTest
    {
        [Fact]
        public void ConstructWithOneParameter()
        {
            var group = new PatternGroup(new string[] { "a" });

            Assert.Equal(new string[] { "a" }, group.IncludePatterns);
            Assert.Equal(0, group.ExcludePatterns.Count());
            Assert.Equal(0, group.IncludeLiterals.Count());
            Assert.Equal(0, group.ExcludePatternsGroup.Count());
        }

        [Fact]
        public void ConstructWithThreeParameters()
        {
            var group = new PatternGroup(new string[] { "a", "b" }, new string[] { "C" }, new string[] { "d" });

            Assert.Equal(new string[] { "a", "b" }, group.IncludePatterns);
            Assert.Equal(new string[] { "C" }, group.ExcludePatterns);
            Assert.Equal(new string[] { "d" }, group.IncludeLiterals);
            Assert.Equal(0, group.ExcludePatternsGroup.Count());
        }

        [Fact]
        public void AddExcludePatternGroup()
        {
            var group1 = new PatternGroup(new string[] { "z" });
            var group2 = new PatternGroup(new string[] { "a", "b" }, new string[] { "C" }, new string[] { "d" });

            group1.ExcludeGroup(group2);

            Assert.Equal(new string[] { "z" }, group1.IncludePatterns);
            Assert.Equal(0, group1.ExcludePatterns.Count());
            Assert.Equal(1, group1.ExcludePatternsGroup.Count());
            Assert.True(PatternsGroupTestHelper.Equals(group2, group1.ExcludePatternsGroup.First()));
        }
    }
}