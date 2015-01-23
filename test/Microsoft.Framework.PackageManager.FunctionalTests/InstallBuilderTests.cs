// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using NuGet;
using Microsoft.Framework.FunctionalTestUtils;
using Xunit;

namespace Microsoft.Framework.PackageManager.FunctionalTests
{
    public class InstallBuilderTests
    {
        [Fact]
        public void ApplicationScriptsHaveTheCorrectContent()
        {
            using (DisposableDir tempDir = new DisposableDir())
            {
                string testDir = Path.Combine(tempDir, "TestApp");
                Directory.CreateDirectory(testDir);

                string projectFilePath = Path.Combine(testDir, "project.json");
                string projectFileContent =
                @"{ 
                    ""commands"" : { 
                        ""cmd1"":""demo1"",
                        ""cmd2"":""demo2""
                    } 
                }";
                File.WriteAllText(projectFilePath, projectFileContent);

                Runtime.Project project;
                Runtime.Project.TryGetProject(projectFilePath, out project);
                
                var packageManager = new MockPackageManager();
                var infoReport = new MockReport();

                var builder = new InstallBuilder(
                    project,
                    packageManager,
                    new Reports()
                    {
                        Information = infoReport
                    });

                Assert.True(builder.Build(testDir));

                ValidateProjectFile(Path.Combine(testDir, "app", "project.json"));
                ValidateScriptFile(Path.Combine(testDir, "app", "cmd1.cmd"));
                ValidateScriptFile(Path.Combine(testDir, "app", "cmd2.cmd"));
            }
        }

        private void ValidateProjectFile(string fullProjectFile)
        {
            Runtime.Project project;
            Runtime.Project.TryGetProject(fullProjectFile, out project);

            Assert.False(project.IsLoadable);
            Assert.Equal("TestApp", project.EntryPoint);
        }

        private void ValidateScriptFile(string fullFilePath)
        {
            Assert.True(File.Exists(fullFilePath));

            string command = Path.GetFileNameWithoutExtension(fullFilePath);

            Assert.Equal(
                string.Format("@dotnet --appbase \"%~dp0.\" Microsoft.Framework.ApplicationHost {0} %*", command),
                File.ReadAllText(fullFilePath));
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

        private class MockPackageManager : IPackageBuilder
        {
            public MockPackageManager()
            {
                Files = new Collection<IPackageFile>();
            }

            public Collection<IPackageFile> Files { get; set; }

            public IEnumerable<string> Authors
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string Copyright
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public IEnumerable<PackageDependencySet> DependencySets
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string Description
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public Uri IconUrl
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string Id
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string Language
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public Uri LicenseUrl
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public Version MinClientVersion
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public IEnumerable<string> Owners
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public ICollection<PackageReferenceSet> PackageAssemblyReferences
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public Uri ProjectUrl
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string ReleaseNotes
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public bool RequireLicenseAcceptance
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string Summary
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string Tags
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public string Title
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public SemanticVersion Version
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public void Save(Stream stream)
            {
                throw new NotImplementedException();
            }
        }
    }
}