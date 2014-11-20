// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Collections.Generic;
using Microsoft.Framework.FunctionalTestUtils;
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
                foreach (var path in TestUtils.GetUnpackedKrePaths())
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackWebApp_RootAsPublicFolder(DisposableDirPath krePath)
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
    '.': ['project.json', 'Config.json', 'Program.cs', 'build_config1.bconfig', 'web.config'],
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

                var exitCode = KpmTestUtils.ExecKpm(
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
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}")
                    .WithFileContents(Path.Combine("wwwroot", "web.config"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""kpm-package-path"" value=""..\approot\packages"" />
    <add key=""bootstrapper-version"" value="""" />
    <add key=""kre-package-path"" value=""..\approot\packages"" />
    <add key=""kre-version"" value="""" />
    <add key=""kre-clr"" value="""" />
    <add key=""kre-app-base"" value=""..\approot\src\PROJECT_NAME"" />
  </appSettings>
</configuration>".Replace("PROJECT_NAME", testEnv.ProjectName));
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackWebApp_SubfolderAsPublicFolder(DisposableDirPath krePath)
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
    'web.config': '',
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

                var exitCode = KpmTestUtils.ExecKpm(
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
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}")
                    .WithFileContents(Path.Combine("wwwroot", "web.config"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""kpm-package-path"" value=""..\approot\packages"" />
    <add key=""bootstrapper-version"" value="""" />
    <add key=""kre-package-path"" value=""..\approot\packages"" />
    <add key=""kre-version"" value="""" />
    <add key=""kre-clr"" value="""" />
    <add key=""kre-app-base"" value=""..\approot\src\PROJECT_NAME"" />
  </appSettings>
</configuration>".Replace("PROJECT_NAME", testEnv.ProjectName));
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackConsoleApp(DisposableDirPath krePath)
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

                var exitCode = KpmTestUtils.ExecKpm(
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
        public void FoldersAsFilePatternsAutoGlob(DisposableDirPath krePath)
        {
            var projectStructure = @"{
  '.': ['project.json', 'FileWithoutExtension'],
  'UselessFolder1': {
    '.': ['file1.txt', 'file2.css', 'file_without_extension'],
    'SubFolder': ['file3.js', 'file4.html', 'file_without_extension']
  },
  'UselessFolder2': {
    '.': ['file1.txt', 'file2.css', 'file_without_extension'],
    'SubFolder': ['file3.js', 'file4.html', 'file_without_extension']
  },
  'UselessFolder3': {
    '.': ['file1.txt', 'file2.css', 'file_without_extension'],
    'SubFolder': ['file3.js', 'file4.html', 'file_without_extension']
  },
  'MixFolder': {
    'UsefulSub': ['useful.txt', 'useful.css', 'file_without_extension'],
    'UselessSub1': ['file1.js', 'file2.html', 'file_without_extension'],
    'UselessSub2': ['file1.js', 'file2.html', 'file_without_extension'],
    'UselessSub3': ['file1.js', 'file2.html', 'file_without_extension'],
    'UselessSub4': ['file1.js', 'file2.html', 'file_without_extension'],
    'UselessSub5': ['file1.js', 'file2.html', 'file_without_extension']
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
          'UsefulSub': ['useful.txt', 'useful.css', 'file_without_extension']
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

                var exitCode = KpmTestUtils.ExecKpm(
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
        public void WildcardMatchingFacts(DisposableDirPath krePath)
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

                var exitCode = KpmTestUtils.ExecKpm(
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
        public void CorrectlyExcludeFoldersStartingWithDots(DisposableDirPath krePath)
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

                var exitCode = KpmTestUtils.ExecKpm(
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

        [Theory]
        [MemberData("KrePaths")]
        public void VerifyDefaultPackExcludePatterns(DisposableDirPath krePath)
        {
            var projectStructure = @"{
  '.': ['project.json', 'File', '.FileStartingWithDot'],
  'bin': {
    'AspNet.Loader.dll': '',
    'Debug': ['test.exe', 'test.dll']
  },
  'obj': {
    'test.obj': '',
    'References': ['ref1.dll', 'ref2.dll']
  },
  '.git': ['index', 'HEAD', 'log'],
  'Folder': {
    '.svn': ['index', 'HEAD', 'log'],
    'File': '',
    '.FileStartingWithDot': ''
  },
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json', 'File', '.FileStartingWithDot'],
        'Folder': ['File', '.FileStartingWithDot']
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

                var exitCode = KpmTestUtils.ExecKpm(
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

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackWebApp_AppendToExistingWebConfig(DisposableDirPath krePath)
        {
            var projectStructure = @"{
  '.': ['project.json', 'web.config'],
  'public': ['index.html'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'wwwroot': ['web.config', 'index.html'],
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': ['project.json', 'web.config']
    }
  }
}".Replace("PROJECT_NAME", _projectName);
            var webConfigContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""non-related-value"" />
  </nonRelatedElement>
</configuration>";

            using (var testEnv = new KpmTestEnvironment(krePath, _projectName, _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{
  ""webroot"": ""public""
}")
                    .WithFileContents("web.config", webConfigContents)
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --wwwroot public --wwwroot-out wwwroot",
                        testEnv.PackOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""webroot"": ""WEB_ROOT""
}".Replace("WEB_ROOT", Path.Combine("..", "..", "..", "wwwroot").Replace(@"\", @"\\")))
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "web.config"), webConfigContents)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}")
                    .WithFileContents(Path.Combine("wwwroot", "web.config"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""non-related-value"" />
  </nonRelatedElement>
  <appSettings>
    <add key=""kpm-package-path"" value=""..\approot\packages"" />
    <add key=""bootstrapper-version"" value="""" />
    <add key=""kre-package-path"" value=""..\approot\packages"" />
    <add key=""kre-version"" value="""" />
    <add key=""kre-clr"" value="""" />
    <add key=""kre-app-base"" value=""..\approot\src\PROJECT_NAME"" />
  </appSettings>
</configuration>".Replace("PROJECT_NAME", testEnv.ProjectName));
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackWebApp_UpdateExistingWebConfig(DisposableDirPath krePath)
        {
            var projectStructure = @"{
  '.': ['project.json', 'web.config'],
  'public': ['index.html'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'wwwroot': ['web.config', 'index.html'],
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': ['project.json', 'web.config']
    }
  }
}".Replace("PROJECT_NAME", _projectName);
            var webConfigContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""OLD_VALUE"" />
  </nonRelatedElement>
  <appSettings>
    <add key=""non-related-key"" value=""OLD_VALUE"" />
    <add key=""kpm-package-path"" value=""OLD_VALUE"" />
    <add key=""bootstrapper-version"" value=""OLD_VALUE"" />
    <add key=""kre-package-path"" value=""OLD_VALUE"" />
    <add key=""kre-version"" value=""OLD_VALUE"" />
    <add key=""kre-clr"" value=""OLD_VALUE"" />
    <add key=""kre-app-base"" value=""OLD_VALUE"" />
  </appSettings>
</configuration>";

            using (var testEnv = new KpmTestEnvironment(krePath, _projectName, _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{
  ""webroot"": ""WEB_ROOT""
}".Replace("WEB_ROOT", Path.Combine("..", "..", "..", "wwwroot").Replace(@"\", @"\\")))
                    .WithFileContents("web.config", webConfigContents)
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --wwwroot public --wwwroot-out wwwroot",
                        testEnv.PackOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = TestUtils.CreateDirTree(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""webroot"": ""WEB_ROOT""
}".Replace("WEB_ROOT", Path.Combine("..", "..", "..", "wwwroot").Replace(@"\", @"\\")))
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "web.config"), webConfigContents)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}")
                    .WithFileContents(Path.Combine("wwwroot", "web.config"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""OLD_VALUE"" />
  </nonRelatedElement>
  <appSettings>
    <add key=""non-related-key"" value=""OLD_VALUE"" />
    <add key=""kpm-package-path"" value=""..\approot\packages"" />
    <add key=""bootstrapper-version"" value="""" />
    <add key=""kre-package-path"" value=""..\approot\packages"" />
    <add key=""kre-version"" value="""" />
    <add key=""kre-clr"" value="""" />
    <add key=""kre-app-base"" value=""..\approot\src\PROJECT_NAME"" />
  </appSettings>
</configuration>".Replace("PROJECT_NAME", testEnv.ProjectName));
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackCanHandleDoubleQuotedProjectPathsWithSpaces(DisposableDirPath krePath)
        {
            var projectStructure = @"{
  '.': ['project.json'],
  'packages': {}
}";

            using (var testEnv = new KpmTestEnvironment(krePath, "Project Name With Spaces", _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("\"{0}\" --out {1}",
                        testEnv.ProjectPath,
                        testEnv.PackOutputDirPath),
                    environment: environment);
                Assert.Equal(0, exitCode);
            }
        }

        [Theory]
        [MemberData("KrePaths")]
        public void KpmPackCanHandleSingleQuotedProjectPathsWithSpaces(DisposableDirPath krePath)
        {
            var projectStructure = @"{
  '.': ['project.json'],
  'packages': {}
}";

            using (var testEnv = new KpmTestEnvironment(krePath, "Project Name With Spaces", _outputDirName))
            {
                TestUtils.CreateDirTree(projectStructure)
                    .WithFileContents("project.json", @"{}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    krePath,
                    subcommand: "pack",
                    arguments: string.Format("'{0}' --out {1}",
                        testEnv.ProjectPath,
                        testEnv.PackOutputDirPath),
                    environment: environment);
                Assert.Equal(0, exitCode);
            }
        }
    }
}
