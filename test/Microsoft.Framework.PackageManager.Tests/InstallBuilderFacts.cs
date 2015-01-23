// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Framework.PackageManager.Tests
{
    public class InstallBuilderFacts
    {
        [Fact]
        public void NoCommandsIsNonApplicationPackage()
        {
            var project = Runtime.Project.GetProject(@"{ }", @"foo", @"c:\foo\project.json");
            var builder = new InstallBuilder(project, null, null);
            Assert.False(builder.IsApplicationPackage);
        }

        [Fact]
        public void ProjectWithCommandsIsApplicationPackage()
        {
            var project = Runtime.Project.GetProject(@"{ ""commands"" : { ""demo"":""demo"" } }", @"foo", @"c:\foo\project.json");
            var builder = new InstallBuilder(project, null, null);
            Assert.True(builder.IsApplicationPackage);
        }

        [Fact]
        public void BuildSucceedsForNonApplicationPackage()
        {
            var project = Runtime.Project.GetProject(@"{ }", @"foo", @"c:\foo\project.json");
            var builder = new InstallBuilder(project, null, null);
            Assert.True(builder.Build(@"c:\foo"));
        }

        [Fact]
        public void NotAllowedCommandNamesAreReportedAndTheBuildFails()
        {
            var project = Runtime.Project.GetProject(@"{ ""commands"" : { ""dotnet"":""demo"" } }", @"foo", @"c:\foo\project.json");

            var errorReport = new MockReport();
            var builder = new InstallBuilder(
                project,
                null,
                new Reports()
                {
                    Error = errorReport
                });

            Assert.False(builder.Build(@"c:\foo"));
            Assert.Equal(1, errorReport.WriteLineCallCount);
            // TODO: Once we use resources, assert the full message
            Assert.True(errorReport.Messages.First().Contains("dotnet"));
        }

        private class MockReport : IReport
        {
            private IList<string> _messages = new List<string>();

            public int WriteLineCallCount { get; private set; }

            public IEnumerable<string> Messages
            {
                get
                {
                    return _messages;
                }
            }

            public void WriteLine(string message)
            {
                WriteLineCallCount++;
                _messages.Add(message);
            }
        }
    }
}