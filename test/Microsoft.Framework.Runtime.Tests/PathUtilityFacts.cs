using System;
using NuGet;
using Xunit;

namespace Microsoft.Framework.Runtime.Tests
{
    public class PathUtilityFacts
    {
        [Fact]
        public void IsChildOfDirectoryWorksWithRelativePath()
        {
            var baseDir = @"..\BaseDir\";
            var childPath = @"..\BaseDir\ChildFile";
            var nonChildPath = @"..\AnotherBaseDir\NonChildFile";

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }

        [Fact]
        public void IsChildOfDirectoryWorksWithAbsolutePath()
        {
            var baseDir = @"C:\Test\BaseDir\";
            var childPath = @"C:\Test\BaseDir\ChildFile";
            var nonChildPath = @"C:\Test\AnotherBaseDir\NonChildFile";

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }

        [Fact]
        public void IsChildOfDirectoryWorksOnBaseDirWithoutTrailingPathSeparator()
        {
            var baseDir = @"..\foo";
            var childPath = @"..\foo\ChildFile";
            var nonChildPath = @"..\food\NonChildFile";

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }
    }
}