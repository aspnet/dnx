// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Framework.Runtime.FileSystem;
using Xunit;

namespace Loader.Tests
{
    public class FileWatcherFacts
    {
        [Fact]
        public void FileChangesAreDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchFile(@"c:\foo.cs");
            watcher.WatchFile(@"c:\bar.cs");

            var changed = watcher.ReportChange(@"c:\foo.cs", WatcherChangeTypes.Changed);

            Assert.True(changed);
        }

        [Fact]
        public void FileDeletionsAreDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchFile(@"c:\foo.cs");
            watcher.WatchFile(@"c:\bar.cs");

            var changed = watcher.ReportChange(@"c:\foo.cs", WatcherChangeTypes.Deleted);

            Assert.True(changed);
        }

        [Fact]
        public void FileAdditionsAreDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");
            var changed = watcher.ReportChange(@"c:\foo.cs", WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Fact]
        public void FileAdditionsForUnwatchedExtensionsAreNotDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");
            var changed = watcher.ReportChange(@"c:\foo.cshtml", WatcherChangeTypes.Created);

            Assert.False(changed);
        }

        [Fact]
        public void NewDirectoryDoesNotReportChange()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");
            var changed = watcher.ReportChange(@"c:\foo", WatcherChangeTypes.Created);

            Assert.False(changed);
        }

        [Fact]
        public void DeletedDirectoryReportsChange()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");
            watcher.WatchDirectory(@"c:\foo", ".cs");

            var changed = watcher.ReportChange(@"c:\foo", WatcherChangeTypes.Deleted);

            Assert.True(changed);
        }

        [Fact]
        public void RenamedFileIsDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");

            var changed = watcher.ReportChange(@"c:\foo.cshtml", @"c:\foo.cs", WatcherChangeTypes.Renamed);

            Assert.True(changed);
        }

        [Fact]
        public void RenamedFromWatchedExtensionIsDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\foo", ".cs");
            watcher.WatchFile(@"c:\foo\foo.cs");

            var changed = watcher.ReportChange(@"c:\foo\foo.cs", @"c:\foo.cshtml", WatcherChangeTypes.Renamed);

            Assert.True(changed);
        }

        [Fact]
        public void NewFolderAndRenameToWatchedExtension()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");

            // Create a folder
            watcher.ReportChange(@"c:\foo", WatcherChangeTypes.Created);

            // Rename a file in that folder
            var changed = watcher.ReportChange(@"c:\foo\foo.cshtml", @"c:\foo\foo.cs", WatcherChangeTypes.Renamed);

            Assert.True(changed);
        }

        [Fact]
        public void NewFolderRenamedWithNewFileAdded()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");

            // Create a folder
            watcher.ReportChange(@"c:\foo", WatcherChangeTypes.Created);

            // Rename that folder
            watcher.ReportChange(@"c:\foo", @"c:\bar", WatcherChangeTypes.Renamed);

            // Create a file in that folder
            var changed = watcher.ReportChange(@"c:\bar\foo.cs", WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Fact]
        public void NewFolderNewFileIsDetected()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");

            // Create a folder
            watcher.ReportChange(@"c:\foo", WatcherChangeTypes.Created);

            // Add a file
            var changed = watcher.ReportChange(@"c:\foo\foo.cs", WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Fact]
        public void NestedFolderCreationAndFileCreationOnFolderUp()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");

            // Create a folder
            watcher.ReportChange(@"c:\foo", WatcherChangeTypes.Created);

            // Create a nested folder
            watcher.ReportChange(@"c:\foo\bar", WatcherChangeTypes.Created);

            // Add a file
            var changed = watcher.ReportChange(@"c:\foo\foo.cs", WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Fact]
        public void NestedFoldersAndFileCreationOnFolderUp()
        {
            var watcher = new FileWatcher();
            watcher.WatchDirectory(@"c:\", ".cs");
            watcher.WatchDirectory(@"c:\a", ".cs");
            watcher.WatchDirectory(@"c:\a\b", ".cs");
            watcher.WatchFile(@"c:\project.json");

            // Add a file
            var changed = watcher.ReportChange(@"c:\a\foo.cs", WatcherChangeTypes.Created);

            Assert.True(changed);
        }

        [Theory]
        [InlineData(@"c:\myprojects\foo", true)]
        [InlineData(@"c:\myprojects\foo\b\c\d.txt", true)]
        [InlineData(@"c:\myprojects\", false)]
        [InlineData(@"c:\myprojects", false)]
        [InlineData(@"c:\myprojects\anotherproject", false)]
        [InlineData(@"c:\myprojects\foos\", false)]
        [InlineData(@"c:\myprojects\foos", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void IsAlreadyWatched(string newPath, bool expectedResult)
        {
            var watcher = new FileWatcher();
            watcher.AddWatcher(new WatcherRoot(@"c:\myprojects\foo"));

            var isAlreadyWatched = watcher.IsAlreadyWatched(newPath);

            Assert.Equal(expectedResult, isAlreadyWatched);
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
