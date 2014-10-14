// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Framework.Runtime;
using Xunit;

namespace Microsoft.Framework.PackageManager
{
    public class KpmPackTests
    {
        private readonly string _projectName = "TestProject";
        private readonly string _outputDirName = "PackOutput";

        public static IEnumerable<object[]> KrePaths
        {
            get
            {
                var kRuntimeRoot = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
                var buildArtifactDir = Path.Combine(kRuntimeRoot, "artifacts", "build");
                foreach (var path in TestUtils.GetUnpackedKrePaths(buildArtifactDir))
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackWebApp_RootAsPublicFolder(string krePath)
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

            using (var testEnv = new KpmTestEnvironment(krePath, _projectName, _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": ""**.bconfig"",
  ""webroot"": ""to_be_overridden""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = TestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --wwwroot . --wwwroot-out wwwroot",
                        testEnv.PackOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""packExclude"": ""**.bconfig"",
  ""webroot"": ""WEB_ROOT""
}".Replace("WEB_ROOT", Path.Combine("..", "..", "..", "wwwroot").Replace(@"\", @"\\")))
                    .WithFileContents(Path.Combine("wwwroot", "project.json"), @"{
  ""packExclude"": ""**.bconfig"",
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

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackWebApp_SubfolderAsPublicFolder(string krePath)
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

            using (var testEnv = new KpmTestEnvironment(krePath, _projectName, _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": ""**.useless"",
  ""webroot"": ""public""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = TestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --wwwroot-out wwwroot",
                        testEnv.PackOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""packExclude"": ""**.useless"",
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

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackConsoleApp(string krePath)
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

            using (var testEnv = new KpmTestEnvironment(krePath, _projectName, _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": ""Data/Backup/**""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = TestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""packExclude"": ""Data/Backup/**""
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void FoldersAsFilePatternsAutoGlob(string krePath)
        {
            var projectStructure = @"{
  '.': ['project.json', 'FileWithoutExtension'],
  'UselessFolder1': {
    '.': ['file1.txt', 'file2.css'],
    'SubFolder': ['file3.js', 'file4.html']
  },
  'UselessFolder2': {
    '.': ['file1.txt', 'file2.css'],
    'SubFolder': ['file3.js', 'file4.html']
  },
  'UselessFolder3': {
    '.': ['file1.txt', 'file2.css'],
    'SubFolder': ['file3.js', 'file4.html']
  },
  'MixFolder': {
    'UsefulSub': ['useful.txt', 'useful.css'],
    'UselessSub1': ['file1.js', 'file2.html'],
    'UselessSub2': ['file1.js', 'file2.html'],
    'UselessSub3': ['file1.js', 'file2.html'],
    'UselessSub4': ['file1.js', 'file2.html'],
    'UselessSub5': ['file1.js', 'file2.html']
  },
  '.git': ['index', 'HEAD', 'log'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json'],
        'MixFolder': {
          'UsefulSub': ['useful.txt', 'useful.css']
        }
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new KpmTestEnvironment(krePath, _projectName, _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": [
    ""FileWithoutExtension"",
    ""UselessFolder1"",
    ""UselessFolder2/"",
    ""UselessFolder3\\"",
    ""MixFolder/UselessSub1/"",
    ""MixFolder\\UselessSub2\\"",
    ""MixFolder/UselessSub3\\"",
    ""MixFolder/UselessSub4"",
    ""MixFolder\\UselessSub5"",
    "".git""
  ]
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = TestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""packExclude"": [
    ""FileWithoutExtension"",
    ""UselessFolder1"",
    ""UselessFolder2/"",
    ""UselessFolder3\\"",
    ""MixFolder/UselessSub1/"",
    ""MixFolder\\UselessSub2\\"",
    ""MixFolder/UselessSub3\\"",
    ""MixFolder/UselessSub4"",
    ""MixFolder\\UselessSub5"",
    "".git""
  ]
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void WildcardMatchingFacts(string krePath)
        {
            var projectStructure = @"{
  '.': ['project.json'],
  'UselessFolder1': {
    '.': ['uselessfile1.txt', 'uselessfile2'],
    'SubFolder': ['uselessfile3.js', 'uselessfile4']
  },
  'UselessFolder2': {
    '.': ['uselessfile1.txt', 'uselessfile2'],
    'SubFolder': ['uselessfile3.js', 'uselessfile4']
  },
  'UselessFolder3': {
    '.': ['uselessfile1.txt', 'uselessfile2'],
    'SubFolder': ['uselessfile3.js', 'uselessfile4']
  },
  'MixFolder1': {
    '.': ['uselessfile1.txt', 'uselessfile2'],
    'UsefulSub': ['useful.txt', 'useful']
  },
  'MixFolder2': {
    '.': ['uselessfile1.txt', 'uselessfile2'],
    'UsefulSub': ['useful.txt', 'useful']
  },
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json'],
        'MixFolder1': {
          'UsefulSub': ['useful.txt', 'useful']
        },
        'MixFolder2': {
          'UsefulSub': ['useful.txt', 'useful']
        }
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new KpmTestEnvironment(krePath, _projectName, _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": [
    ""UselessFolder1\\**"",
    ""UselessFolder2/**/*"",
    ""UselessFolder3\\**/*.*"",
    ""MixFolder1\\*"",
    ""MixFolder2/*.*""
  ]
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = TestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""packExclude"": [
    ""UselessFolder1\\**"",
    ""UselessFolder2/**/*"",
    ""UselessFolder3\\**/*.*"",
    ""MixFolder1\\*"",
    ""MixFolder2/*.*""
  ]
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }
        
        [Theory]
        [MemberData("KrePaths")]
        public void CorrectlyExcludeFoldersStartingWithDots(string krePath)
        {
            var projectStructure = @"{
  '.': ['project.json', 'File', '.FileStartingWithDot', 'File.Having.Dots'],
  '.FolderStaringWithDot': {
    'SubFolder': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    '.SubFolderStartingWithDot': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    'SubFolder.Having.Dots': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    'File': '',
    '.FileStartingWithDot': '',
    'File.Having.Dots': ''
  },
  'Folder': {
    'SubFolder': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    '.SubFolderStartingWithDot': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    'SubFolder.Having.Dots': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    'File': '',
    '.FileStartingWithDot': '',
    'File.Having.Dots': ''
  },
  'Folder.Having.Dots': {
    'SubFolder': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    '.SubFolderStartingWithDot': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    'SubFolder.Having.Dots': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
    'File': '',
    '.FileStartingWithDot': '',
    'File.Having.Dots': ''
  },
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json', 'File', '.FileStartingWithDot', 'File.Having.Dots'],
        'Folder': {
          'SubFolder': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
          'SubFolder.Having.Dots': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
          'File': '',
          '.FileStartingWithDot': '',
          'File.Having.Dots': ''
        },
        'Folder.Having.Dots': {
          'SubFolder': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
          'SubFolder.Having.Dots': ['File', '.FileStartingWithDot', 'File.Having.Dots'],
          'File': '',
          '.FileStartingWithDot': '',
          'File.Having.Dots': ''
        }
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new KpmTestEnvironment(krePath, _projectName, _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = TestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
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
