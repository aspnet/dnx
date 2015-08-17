using System;
using System.Collections.Generic;
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

        public static IEnumerable<object[]> FolderPaths
        {
            get
            {
                var list =  new List<object[]>
                {
                    new object[] { "", "/folder/app", "/folder/app" },
                    new object[] { "../app", "/folder/folder/app", "/folder/app" },
                    new object[] { "", "/folder/app/", "/folder/app/" },
                    new object[] { "../../diff/app", "/folder/folder/app", "/diff/app" },
                    new object[] { "../t/app", "/folder/s/app", "/folder/t/app" },
                    new object[] { "app/", "/folder/s/app", "/folder/s/app/" },
                    new object[] { "", "/folder/app/f", "/folder/app/" },
                    new object[] { "../", "/folder/app/f/", "/folder/app/" },
                    new object[] { "app1", "/folder/app2", "/folder/app1" },
                    new object[] { "app", "/folder/", "/folder/app" },
                    new object[] { "../folder", "/folder/folder/app", "/folder/folder"}
                };

                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    list.AddRange(new List<object[]>
                    {
                        new object[] { "../app", "/FOLDER/folder/APP", "/folder/app" },
                        new object[] { "", "C:/folder/app", "C:/folder/app" },
                        new object[] { "app/", "C:/folder/app", "C:/folder/app/" },
                        new object[] { "../diff/app/", "C:/folder/app", "C:/diff/app/" },
                        new object[] { "../folder1/file", "C:\\folder\\file", "C:\\folder1\\file" }
                    });
                }
                else
                {
                    list.AddRange(new List<object[]>
                    {
                        new object[] { "../../folder/app", "/FOLDER/folder/APP", "/folder/app" },
                        new object[] { "../../FOLDER/app", "/folder/folder/APP", "/FOLDER/app" },
                        new object[] { "../folder/file\\name", "/home/user/file\\name/app", "/home/user/folder/file\\name" }
                    });
                }

                return list;
            }
        }

        [Theory]
        [MemberData(nameof(FolderPaths))]
        public void GetRelativePathReturnsCorrectRelativePaths(string expected, string path1, string path2)
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                expected = PathUtility.GetPathWithBackSlashes(expected);
            }
            Assert.Equal(expected, PathUtility.GetRelativePath(path1, path2));
        }
    }
}