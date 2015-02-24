// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Framework.Runtime.Hosting;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests.FileGlobbing
{
    public class PatternsGroupTest
    {
        [Fact]
        public void ConstructWithOneParameter()
        {
            var group = new PatternsGroup(new string[] { "a" });

            Assert.Equal(new string[] { "a" }, group.IncludePatterns);
            Assert.Equal(0, group.ExcludePatterns.Count());
            Assert.Equal(0, group.IncludeLiterals.Count());
            Assert.Equal(0, group.ExcludePatternsGroup.Count());
        }

        [Fact]
        public void ConstructWithThreeParameters()
        {
            var group = new PatternsGroup(new string[] { "a", "b" }, new string[] { "C" }, new string[] { "d" });

            Assert.Equal(new string[] { "a", "b" }, group.IncludePatterns);
            Assert.Equal(new string[] { "C" }, group.ExcludePatterns);
            Assert.Equal(new string[] { "d" }, group.IncludeLiterals);
            Assert.Equal(0, group.ExcludePatternsGroup.Count());
        }

        [Fact]
        public void AddExcludePatternGroup()
        {
            var group1 = new PatternsGroup(new string[] { "z" });
            var group2 = new PatternsGroup(new string[] { "a", "b" }, new string[] { "C" }, new string[] { "d" });

            group1.ExcludeGroup(group2);

            Assert.Equal(new string[] { "z" }, group1.IncludePatterns);
            Assert.Equal(0, group1.ExcludePatterns.Count());
            Assert.Equal(1, group1.ExcludePatternsGroup.Count());
            Assert.True(PatternsGroupTestHelper.Equals(group2, group1.ExcludePatternsGroup.First()));
        }
    }
}