// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Compilation.FileSystem;
using Xunit;

namespace Loader.Tests
{
    public class FileWatcherFacts
    {
        [Fact]
        public void FileChangesAreDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchFile(PathHelpers.GetRootedPath("foo.cs"));
            watcher.WatchFile(PathHelpers.GetRootedPath("bar.cs"));

            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo.cs"), WatcherChangeTypes.Changed);

            Assert.True(changed);
        }

        [Fact]
        public void FileDeletionsAreDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchFile(PathHelpers.GetRootedPath("foo.cs"));
            watcher.WatchFile(PathHelpers.GetRootedPath("bar.cs"));

            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo.cs"), WatcherChangeTypes.Deleted);

            Assert.True(changed);
        }

        [Fact]
        public void FileAdditionsAreDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");
            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo.cs"), WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Fact]
        public void FileAdditionsForUnwatchedExtensionsAreNotDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");
            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo.cshtml"), WatcherChangeTypes.Created);

            Assert.False(changed);
        }

        [Fact]
        public void NewDirectoryDoesNotReportChange()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");
            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo"), WatcherChangeTypes.Created);

            Assert.False(changed);
        }

        [Fact]
        public void DeletedDirectoryReportsChange()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");
            watcher.WatchDirectory(PathHelpers.GetRootedPath("foo"), ".cs");

            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo"), WatcherChangeTypes.Deleted);

            Assert.True(changed);
        }

        [Fact]
        public void RenamedFileIsDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");

            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo.cshtml"), PathHelpers.GetRootedPath("foo.cs"), WatcherChangeTypes.Renamed);

            Assert.True(changed);
        }

        [Fact]
        public void RenamedFromWatchedExtensionIsDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath("foo"), ".cs");
            watcher.WatchFile(PathHelpers.GetRootedPath("foo", "foo.cs"));

            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo", "foo.cs"), 
                                               PathHelpers.GetRootedPath("foo.cshtml"), 
                                               WatcherChangeTypes.Renamed);

            Assert.True(changed);
        }

        [Fact]
        public void NewFolderAndRenameToWatchedExtension()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");

            // Create a folder
            watcher.ReportChange(PathHelpers.GetRootedPath("foo"), WatcherChangeTypes.Created);

            // Rename a file in that folder
            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo", "foo.cshtml"), 
                                               PathHelpers.GetRootedPath("foo", "foo.cs"), 
                                               WatcherChangeTypes.Renamed);

            Assert.True(changed);
        }

        [Fact]
        public void NewFolderRenamedWithNewFileAdded()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");

            // Create a folder
            watcher.ReportChange(PathHelpers.GetRootedPath("foo"), WatcherChangeTypes.Created);

            // Rename that folder
            watcher.ReportChange(PathHelpers.GetRootedPath("foo"), 
                                 PathHelpers.GetRootedPath("bar"), WatcherChangeTypes.Renamed);

            // Create a file in that folder
            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("bar", "foo.cs"), WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Fact]
        public void NewFolderNewFileIsDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");

            // Create a folder
            watcher.ReportChange(PathHelpers.GetRootedPath("foo"), WatcherChangeTypes.Created);

            // Add a file
            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo", "foo.cs"), WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Fact]
        public void NestedFolderCreationAndFileCreationOnFolderUp()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");

            // Create a folder
            watcher.ReportChange(PathHelpers.GetRootedPath("foo"), WatcherChangeTypes.Created);

            // Create a nested folder
            watcher.ReportChange(PathHelpers.GetRootedPath("foo", "bar"), WatcherChangeTypes.Created);

            // Add a file
            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("foo", "foo.cs"), WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Fact]
        public void NestedFoldersAndFileCreationOnFolderUp()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(PathHelpers.GetRootedPath(), ".cs");
            watcher.WatchDirectory(PathHelpers.GetRootedPath("a"), ".cs");
            watcher.WatchDirectory(PathHelpers.GetRootedPath("a", "b"), ".cs");
            watcher.WatchFile(PathHelpers.GetRootedPath("project.json"));

            // Add a file
            var changed = watcher.ReportChange(PathHelpers.GetRootedPath("a", "foo.cs"), WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Theory]
        [MemberData("TestPaths")]
        public void IsAlreadyWatched(string newPath, bool expectedResult)
        {
            var watcher = new FileWatcher();
            watcher.AddWatcher(new WatcherRoot(PathHelpers.GetRootedPath("myprojects", "foo")));

            var isAlreadyWatched = watcher.IsAlreadyWatched(newPath);

            Assert.Equal(expectedResult, isAlreadyWatched);
        }

        public static IEnumerable<object[]> TestPaths
        {
            get {
                yield return new object [] { PathHelpers.GetRootedPath("myprojects", "foo"), true  };
                yield return new object [] { PathHelpers.GetRootedPath("myprojects", "foo", "b", "c", "d.txt"), true  };
                yield return new object [] { PathHelpers.GetRootedPath("myprojects") + Path.DirectorySeparatorChar, false  };
                yield return new object [] { PathHelpers.GetRootedPath("myprojects"), false  };
                yield return new object [] { PathHelpers.GetRootedPath("myprojects", "anotherproject"), false };
                yield return new object [] { PathHelpers.GetRootedPath("myprojects", "foos") + Path.DirectorySeparatorChar, false };
                yield return new object [] { PathHelpers.GetRootedPath("myprojects", "foos"), false };
                yield return new object [] { null, false };
                yield return new object [] { "", false };
            }
        }

        private class WatcherRoot : IWatcherRoot
        {
            public WatcherRoot(string path)
            {
                Path = path;
            }
            public string Path { get; private set; }

            public void Dispose()
            {

            }
        }
    }
}
