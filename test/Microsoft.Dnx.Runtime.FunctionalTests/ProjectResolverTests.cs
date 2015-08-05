// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Dnx.CommonTestUtils;
using Xunit;

namespace Microsoft.Dnx.Runtime.FunctionalTests
{
    public class ProjectResolverTests
    {
        [Fact]
        public void ProjectResolverThrowsWhenResolvingAmbiguousName()
        {
            const string ambiguousName = "ProjectA";
            var solutionStructure = @"{
  'global.json': '',
  'src1': {
    'ProjectA': {
      'project.json': '{}'
    }
  },
  'src2': {
    'ProjectA': {
      'project.json': '{}'
    }
  }
}";

            using (var solutionPath = new DisposableDir())
            {
                DirTree.CreateFromJson(solutionStructure)
                    .WithFileContents("global.json", @"{
  ""projects"": [""src1"", ""src2""]
}")
                    .WriteTo(solutionPath);

                var src1ProjectPath = Path.Combine(solutionPath, "src1", ambiguousName);
                var src2ProjectPath = Path.Combine(solutionPath, "src2", ambiguousName);
                var expectedMessage = $@"The project name '{ambiguousName}' is ambiguous between the following projects:
{src1ProjectPath}
{src2ProjectPath}";

                Project project = null;
                var resolver1 = new PathSearchBasedProjectResolver(src1ProjectPath);
                var exception = Assert.Throws<InvalidOperationException>(() => resolver1.TryResolveProject(ambiguousName, out project));
                Assert.Contains(expectedMessage, exception.Message);
                Assert.Null(project);

                var resolver2 = new PathSearchBasedProjectResolver(src2ProjectPath);
                exception = Assert.Throws<InvalidOperationException>(() => resolver2.TryResolveProject(ambiguousName, out project));
                Assert.Contains(expectedMessage, exception.Message);
                Assert.Null(project);
            }
        }

        [Fact]
        public void ProjectResolverChecksProjectFileForDisambiguation()
        {
            const string projectName = "ProjectA";
            var solutionStructure = @"{
  'global.json': '',
  'src1': {
    'ProjectA': {
      'project.json': '{}'
    }
  },
  'src2': {
    'ProjectA': {
      'file.txt': 'Not a project.json'
    }
  }
}";

            using (var solutionPath = new DisposableDir())
            {
                DirTree.CreateFromJson(solutionStructure)
                    .WithFileContents("global.json", @"{
  ""projects"": [""src1"", ""src2""]
}")
                    .WriteTo(solutionPath);

                var projectPath = Path.Combine(solutionPath, "src1", projectName);

                Project project;
                Assert.True(new PathSearchBasedProjectResolver(projectPath).TryResolveProject(projectName, out project));
                Assert.NotNull(project);
            }
        }

        [Fact]
        public void ProjectResolverDoesNotThrowWhenAmbiguousNameIsNotUsed()
        {
            const string ambiguousName = "ProjectA";
            const string unambiguousName = "ProjectB";
            var solutionStructure = @"{
  'global.json': '',
  'src1': {
    'ProjectA': {
      'project.json': '{}'
    },
    'ProjectB': {
      'project.json': '{}'
    }
  },
  'src2': {
    'ProjectA': {
      'project.json': '{}'
    }
  }
}";

            using (var solutionPath = new DisposableDir())
            {
                DirTree.CreateFromJson(solutionStructure)
                    .WithFileContents("global.json", @"{
  ""projects"": [""src1"", ""src2""]
}")
                    .WriteTo(solutionPath);

                var ambiguousProjectPath = Path.Combine(solutionPath, "src1", ambiguousName);
                var unambiguousProjectPath = Path.Combine(solutionPath, "src1", unambiguousName);

                Project project;
                Assert.True(new PathSearchBasedProjectResolver(ambiguousProjectPath).TryResolveProject(unambiguousName, out project));
                Assert.NotNull(project);

                project = null;
                Assert.True(new PathSearchBasedProjectResolver(unambiguousProjectPath).TryResolveProject(unambiguousName, out project));
                Assert.NotNull(project);
            }
        }

        [Fact]
        public void ProjectResolverWorksWithMultipleNonProjectFoldersThatHaveSameName()
        {
            const string projectName = "ProjectA";
            var solutionStructure = @"{
  'global.json': '',
  'src1': {
    'ProjectA': {
      'file.txt': 'Not a project.json'
    }
  },
  'src2': {
    'ProjectA': {
      'file.txt': 'Not a project.json'
    }
  }
}";

            using (var solutionPath = new DisposableDir())
            {
                DirTree.CreateFromJson(solutionStructure)
                    .WithFileContents("global.json", @"{
  ""projects"": [""src1"", ""src2""]
}")
                    .WriteTo(solutionPath);

                var projectPath = Path.Combine(solutionPath, "src1", projectName);

                Project project;
                Assert.False(new PathSearchBasedProjectResolver(projectPath).TryResolveProject(projectName, out project));
                Assert.Null(project);
            }
        }

        [Fact]
        public void ProjectResolverDoesNotThrowWhenThereAreDuplicatedEntriesInGlobalJson()
        {
            const string unambiguousName = "ProjectA";
            var solutionStructure = @"{
  'global.json': '',
  'src': {
    'ProjectA': {
      'project.json': '{}'
    }
  }
}";

            using (var solutionPath = new DisposableDir())
            {
                DirTree.CreateFromJson(solutionStructure)
                    .WithFileContents("global.json", @"{
  ""projects"": [""src"", ""src/../src"", ""somedir\\somesubdir\\..\\..\\src""]
}")
                    .WriteTo(solutionPath);

                var unambiguousProjectPath = Path.Combine(solutionPath, "src", unambiguousName);

                Project project;
                Assert.True(new PathSearchBasedProjectResolver(unambiguousProjectPath).TryResolveProject(unambiguousName, out project));
                Assert.NotNull(project);
            }
        }

        [Fact]
        public void CanSpecifyProjectDirectoryInGlobalJson()
        {
            var solutionStructure = @"{
  'global.json': '',
  'src': {
    'ProjectA': {
      'project.json': '{}'
    }
  },
  'ProjectB': {
    'project.json': '{}'
  }
}";

            using (var solutionPath = new DisposableDir())
            {
                DirTree.CreateFromJson(solutionStructure)
                    .WithFileContents("global.json", @"{
  ""projects"": [""src"", ""ProjectB""]
}")
                    .WriteTo(solutionPath);

                var resolutionRoot = Path.Combine(solutionPath, "src", "ProjectA");
                var resolver = new PathSearchBasedProjectResolver(resolutionRoot);

                Project project;
                var result = resolver.TryResolveProject("ProjectB", out project);

                Assert.True(result);
                Assert.NotNull(project);
            }
        }
    }
}
