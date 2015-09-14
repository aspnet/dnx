// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.Testing.SampleTests
{
    [Collection(nameof(SampleTestCollection))]
    public class DnuCommonUtilTests : DnxSdkFunctionalTestBase
    {
        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void FailTest(DnxSdk sdk)
        {
            var feedSolution = TestUtils.GetSolution<DnuCommonUtilTests>(sdk, "DependencyGraphsProject");

            //Assert.True(false);

            TestUtils.CleanUpTestDir<DnuCommonUtilTests>(sdk);
        }

        [Theory, TraceTest]
        [MemberData(nameof(DnxSdks))]
        public void ComparisonTest(DnxSdk sdk)
        {
            var file1a = new JObject
            {
                ["info"] = "foo"
            };

            var file1b = new JObject
            {
                ["info"] = "bar1"
            };

            var project1 = new Dir
            {
                ["file1"] = file1a,
                ["file2"] = file1b
            };

            var file2a = new JObject
            {
                ["info"] = "foo"
            };

            var file2b = new JObject
            {
                ["info"] = "bar2"
            };

            var project2 = new Dir
            {
                ["file1"] = file2a,
                ["file2"] = file2b
            };

            var project3 = new Dir
            {
                ["file1"] = file2a,
                ["file2"] = new DirItem(Dir.EmptyFile, skipComparison: true)
            };

            var project4 = new Dir
            {
                ["subdir1"] = project1,
                ["subdir2"] = project1
            };

            var project5 = new Dir
            {
                ["subdir1"] = new DirItem(project2, skipComparison: true),
                ["subdir2"] = project2
            };

            DirAssert.Equal(project1, project2, compareContents: false);
            DirAssert.Equal(project1, project3, compareContents: false);
            DirAssert.Equal(project1, project3);
            DirAssert.Equal(project2, project3);
            //DirAssert.Equal(project4, project5); // this should fail, but only subdir2 should be different

            TestUtils.CleanUpTestDir<DnuPublishTests>(sdk);
        }
    }
}
