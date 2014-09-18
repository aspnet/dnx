// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Framework.Runtime;
using Xunit;

namespace Microsoft.Framework.PackageManager
{
    public class KpmPackTests : IDisposable
    {
        private readonly string _projectName = "TestProject";
        private readonly string _outputDirName = "PackOutput";
        private readonly string _frameworksRoot;

        public KpmPackTests()
        {
            _frameworksRoot = TestUtils.CreateTempDir();
            var kRuntimeRoot = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var buildArtifactDir = Path.Combine(kRuntimeRoot, "artifacts", "build");
            TestUtils.UnpackFrameworksToDir(buildArtifactDir, destination: _frameworksRoot);
        }

        public void Dispose()
        {
            TestUtils.DeleteFolder(_frameworksRoot);
        }

        [Fact]
        public void KpmPackWebApp_RootAsPublicFolder()
        {
            var projectStructure = @"{
  '.': ['project.json', 'Config.json', 'Program.cs', 'build_config1.bconfig'],
  'Views': {
    'Home': ['index.cshtml'],
    'Shared': ['_Layout.cshtml']
  },
  'Controllers': ['HomeController.cs'],
  'Models': ['User.cs', 'build_config2.bconfig'],
  'Build': ['build_config3.bconfig'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'wwwroot': {
    '.': ['project.json', 'Config.json', 'Program.cs', 'build_config1.bconfig', 'k.ini'],
      'Views': {
        'Home': ['index.cshtml'],
        'Shared': ['_Layout.cshtml']
    },
    'Controllers': ['HomeController.cs'],
    'Models': ['User.cs', 'build_config2.bconfig'],
    'Build': ['build_config3.bconfig']
  },
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json', 'Config.json', 'Program.cs'],
          'Views': {
            'Home': ['index.cshtml'],
            'Shared': ['_Layout.cshtml']
        },
        'Controllers': ['HomeController.cs'],
        'Models': ['User.cs']
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            foreach (var framework in Directory.EnumerateDirectories(_frameworksRoot))
            {
                using (var testEnv = new KpmTestEnvironment(_projectName, _outputDirName))
                {
                    TestUtils.CreateDirTree(projectStructure)
                        .WithFileContents("project.json", @"{
  ""pack-exclude"": ""**.bconfig"",
  ""webroot"": ""to_be_overridden""
}")
                        .WriteTo(testEnv.ProjectPath);

                    var environment = new Dictionary<string, string>()
                    {
                        { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                    };

                    var exitCode = TestUtils.ExecKpm(
                        krePath: framework,
                        subcommand: "pack",
                        arguments: string.Format("--out {0} --wwwroot . --wwwroot-out wwwroot",
                            testEnv.PackOutputDirPath),
                        environment: environment,
                        workingDir: testEnv.ProjectPath);
                    Assert.Equal(0, exitCode);

                    var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                        .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""pack-exclude"": ""**.bconfig"",
  ""webroot"": ""WEB_ROOT""
}".Replace("WEB_ROOT", Path.Combine("..", "..", "..", "wwwroot").Replace(@"\", @"\\")))
                        .WithFileContents(Path.Combine("wwwroot", "project.json"), @"{
  ""pack-exclude"": ""**.bconfig"",
  ""webroot"": ""to_be_overridden""
}")
                        .WithFileContents(Path.Combine("wwwroot", "k.ini"), @"KRE_APPBASE=..\approot\src\"
                            + testEnv.ProjectName)
                        .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                    Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                        compareFileContents: true));
                }
            }
        }

        [Fact]
        public void KpmPackWebApp_SubfolderAsPublicFolder()
        {
            var projectStructure = @"{
  '.': ['project.json', 'Config.json', 'Program.cs'],
  'public': {
    'Scripts': ['bootstrap.js', 'jquery.js'],
    'Images': ['logo.png'],
    'UselessFolder': ['file.useless']
  },
  'Views': {
    'Home': ['index.cshtml'],
    'Shared': ['_Layout.cshtml']
  },
  'Controllers': ['HomeController.cs'],
  'UselessFolder': ['file.useless'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'wwwroot': {
    'k.ini': '',
    'Scripts': ['bootstrap.js', 'jquery.js'],
    'Images': ['logo.png'],
    'UselessFolder': ['file.useless']
  },
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json', 'Config.json', 'Program.cs'],
          'Views': {
            'Home': ['index.cshtml'],
            'Shared': ['_Layout.cshtml']
        },
        'Controllers': ['HomeController.cs'],
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            foreach (var framework in Directory.EnumerateDirectories(_frameworksRoot))
            {
                using (var testEnv = new KpmTestEnvironment(_projectName, _outputDirName))
                {
                    TestUtils.CreateDirTree(projectStructure)
                        .WithFileContents("project.json", @"{
  ""pack-exclude"": ""**.useless"",
  ""webroot"": ""public""
}")
                        .WriteTo(testEnv.ProjectPath);

                    var environment = new Dictionary<string, string>()
                    {
                        { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                    };

                    var exitCode = TestUtils.ExecKpm(
                        krePath: framework,
                        subcommand: "pack",
                        arguments: string.Format("--out {0} --wwwroot-out wwwroot",
                            testEnv.PackOutputDirPath),
                        environment: environment,
                        workingDir: testEnv.ProjectPath);
                    Assert.Equal(0, exitCode);

                    var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                        .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""pack-exclude"": ""**.useless"",
  ""webroot"": ""WEB_ROOT""
}".Replace("WEB_ROOT", Path.Combine("..", "..", "..", "wwwroot").Replace(@"\", @"\\")))
                        .WithFileContents(Path.Combine("wwwroot", "k.ini"), @"KRE_APPBASE=..\approot\src\"
                            + testEnv.ProjectName)
                        .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                    Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                        compareFileContents: true));
                }
            }
        }

        [Fact]
        public void KpmPackConsoleApp()
        {
            var projectStructure = @"{
  '.': ['project.json', 'Config.json', 'Program.cs'],
  'Data': {
    'Input': ['data1.dat', 'data2.dat'],
    'Backup': ['backup1.dat', 'backup2.dat']
  },
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json', 'Config.json', 'Program.cs'],
          'Data': {
            'Input': ['data1.dat', 'data2.dat']
          }
        }
      }
    }
  }".Replace("PROJECT_NAME", _projectName);

            foreach (var framework in Directory.EnumerateDirectories(_frameworksRoot))
            {
                using (var testEnv = new KpmTestEnvironment(_projectName, _outputDirName))
                {
                    TestUtils.CreateDirTree(projectStructure)
                        .WithFileContents("project.json", @"{
  ""pack-exclude"": ""Data/Backup/**""
}")
                        .WriteTo(testEnv.ProjectPath);

                    var environment = new Dictionary<string, string>()
                    {
                        { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                    };

                    var exitCode = TestUtils.ExecKpm(
                        krePath: framework,
                        subcommand: "pack",
                        arguments: string.Format("--out {0}",
                            testEnv.PackOutputDirPath),
                        environment: environment,
                        workingDir: testEnv.ProjectPath);
                    Assert.Equal(0, exitCode);

                    var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                        .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""pack-exclude"": ""Data/Backup/**""
}")
                        .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                    Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                        compareFileContents: true));
                }
            }
        }
    }
}
