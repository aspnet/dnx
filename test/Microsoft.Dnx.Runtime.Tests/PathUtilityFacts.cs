using System;
using System.IO;
using Microsoft.Dnx.CommonTestUtils;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class PathUtilityFacts
    {
        [Fact]
        public void IsChildOfDirectoryWorksWithRelativePath()
        {
            var baseDir = Path.Combine("..", "BaseDir") + Path.DirectorySeparatorChar;
            var childPath = Path.Combine("..", "BaseDir", "ChildFile");
            var nonChildPath = Path.Combine("..", "AnotherBaseDir", "NonChildFile");

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }

        [Fact]
        public void IsChildOfDirectoryWorksWithAbsolutePath()
        {
            var baseDir = PathHelpers.GetRootedPath("Test", "BaseDir") + Path.DirectorySeparatorChar;
            var childPath = PathHelpers.GetRootedPath("Test", "BaseDir", "ChildFile");
            var nonChildPath = PathHelpers.GetRootedPath("Test", "AnotherBaseDir", "NonChildFile");

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }

        [Fact]
        public void IsChildOfDirectoryWorksOnBaseDirWithoutTrailingPathSeparator()
        {
            var baseDir = Path.Combine("..", "foo");
            var childPath = Path.Combine("..", "foo", "ChildFile");
            var nonChildPath = Path.Combine("..", "food", "NonChildFile");

            Assert.True(PathUtility.IsChildOfDirectory(baseDir, childPath));
            Assert.False(PathUtility.IsChildOfDirectory(baseDir, nonChildPath));
        }
    }
}