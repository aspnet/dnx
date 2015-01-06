// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.FunctionalTestUtils;
using Xunit;

namespace Microsoft.Framework.PackageManager
{
    public class KpmPackTests
    {
        private readonly string _projectName = "TestProject";
        private readonly string _outputDirName = "BundleOutput";

        private static readonly string BatchFileTemplate = @"
@""{0}klr.exe"" --appbase ""%~dp0approot\src\{1}"" Microsoft.Framework.ApplicationHost {2} %*
";

        private static readonly string BashScriptTemplate = @"#!/bin/bash

SOURCE=""${{BASH_SOURCE[0]}}""
while [ -h ""$SOURCE"" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""
  SOURCE=""$(readlink ""$SOURCE"")""
  [[ $SOURCE != /* ]] && SOURCE=""$DIR/$SOURCE"" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""

export SET KRE_APPBASE=""$DIR/approot/src/{0}""

exec ""{1}klr"" Microsoft.Framework.ApplicationHost {2} ""$@""".Replace("\r\n", "\n");

        public static IEnumerable<object[]> KreHomeDirs
        {
            get
            {
                foreach (var path in TestUtils.GetKreHomeDirs())
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KpmBundleWebApp_RootAsPublicFolder(DisposableDir kreHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
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
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0} --wwwroot . --wwwroot-out wwwroot",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""packExclude"": ""**.bconfig"",
  ""webroot"": ""../../../wwwroot""
}")
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
    <add key=""kre-app-base"" value=""..\approot\src\{0}"" />
  </appSettings>
</configuration>", testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KpmBundleWebApp_SubfolderAsPublicFolder(DisposableDir kreHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
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
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0} --wwwroot-out wwwroot",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""packExclude"": ""**.useless"",
  ""webroot"": ""../../../wwwroot""
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
    <add key=""kre-app-base"" value=""..\approot\src\{0}"" />
  </appSettings>
</configuration>", testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KpmBundleConsoleApp(DisposableDir kreHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": ""Data/Backup/**""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""packExclude"": ""Data/Backup/**""
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void FoldersAsFilePatternsAutoGlob(DisposableDir kreHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
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
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
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
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void WildcardMatchingFacts(DisposableDir kreHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
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
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
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
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void CorrectlyExcludeFoldersStartingWithDots(DisposableDir kreHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void VerifyDefaultBundleExcludePatterns(DisposableDir kreHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KpmBundleWebApp_AppendToExistingWebConfig(DisposableDir kreHomeDir)
        {
            var projectStructure = @"{
  '.': ['project.json'],
  'public': ['index.html', 'web.config'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'wwwroot': ['web.config', 'index.html'],
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': ['project.json']
    }
  }
}".Replace("PROJECT_NAME", _projectName);
            var webConfigContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""non-related-value"" />
  </nonRelatedElement>
</configuration>";

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""webroot"": ""public""
}")
                    .WithFileContents(Path.Combine("public", "web.config"), webConfigContents)
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0} --wwwroot public --wwwroot-out wwwroot",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""webroot"": ""../../../wwwroot""
}")
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
    <add key=""kre-app-base"" value=""..\approot\src\{0}"" />
  </appSettings>
</configuration>", testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void KpmBundleWebApp_UpdateExistingWebConfig(DisposableDir kreHomeDir)
        {
            var projectStructure = @"{
  '.': ['project.json'],
  'public': ['index.html', 'web.config'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  'wwwroot': ['web.config', 'index.html'],
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': ['project.json']
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

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""webroot"": ""../../../wwwroot""
}")
                    .WithFileContents(Path.Combine("public", "web.config"), webConfigContents)
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0} --wwwroot public --wwwroot-out wwwroot",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""webroot"": ""../../../wwwroot""
}")
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
    <add key=""kre-app-base"" value=""..\approot\src\{0}"" />
  </appSettings>
</configuration>", testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void GenerateBatchFilesAndBashScriptsWithoutBundledRuntime(DisposableDir kreHomeDir)
        {
            var projectStructure = @"{
  '.': ['project.json'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  '.': ['run.cmd', 'run', 'kestrel.cmd', 'kestrel'],
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json']
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""commands"": {
    ""run"": ""run server.urls=http://localhost:5003"",
    ""kestrel"": ""Microsoft.AspNet.Hosting --server Kestrel --server.urls http://localhost:5004""
  },
  ""frameworks"": {
    ""aspnet50"": { },
    ""aspnetcore50"": { }
  }
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""commands"": {
    ""run"": ""run server.urls=http://localhost:5003"",
    ""kestrel"": ""Microsoft.AspNet.Hosting --server Kestrel --server.urls http://localhost:5004""
  },
  ""frameworks"": {
    ""aspnet50"": { },
    ""aspnetcore50"": { }
  }
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}")
                    .WithFileContents("run.cmd", BatchFileTemplate, string.Empty, testEnv.ProjectName, "run")
                    .WithFileContents("kestrel.cmd", BatchFileTemplate, string.Empty, testEnv.ProjectName, "kestrel")
                    .WithFileContents("run",
                        BashScriptTemplate, testEnv.ProjectName, string.Empty, "run")
                    .WithFileContents("kestrel",
                        BashScriptTemplate, testEnv.ProjectName, string.Empty, "kestrel");

                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("KreHomeDirs")]
        public void GenerateBatchFilesAndBashScriptsWithBundledRuntime(DisposableDir kreHomeDir)
        {
            // Each KRE home only contains one KRE package, which is the one we are currently testing against
            var kreRoot = Directory.EnumerateDirectories(Path.Combine(kreHomeDir, "packages"), "KRE-*").First();
            var kreName = new DirectoryInfo(kreRoot).Name;

            var projectStructure = @"{
  '.': ['project.json'],
  'packages': {}
}";
            var expectedOutputStructure = @"{
  '.': ['run.cmd', 'run', 'kestrel.cmd', 'kestrel'],
  'approot': {
    'global.json': '',
    'src': {
      'PROJECT_NAME': {
        '.': ['project.json']
      }
    },
    'packages': {
      'KRE_PACKAGE_NAME': {}
    }
  }
}".Replace("PROJECT_NAME", _projectName).Replace("KRE_PACKAGE_NAME", kreName);

            using (var testEnv = new KpmTestEnvironment(kreHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""commands"": {
    ""run"": ""run server.urls=http://localhost:5003"",
    ""kestrel"": ""Microsoft.AspNet.Hosting --server Kestrel --server.urls http://localhost:5004""
  },
  ""frameworks"": {
    ""aspnet50"": { },
    ""aspnetcore50"": { }
  }
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "KRE_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") },
                    { "KRE_HOME", kreHomeDir },
                    { "KRE_TRACE", "1" }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    kreHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0} --runtime {1}",
                        testEnv.BundleOutputDirPath, kreName),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var kreNupkgSHA = TestUtils.ComputeSHA(Path.Combine(kreRoot, kreName + ".nupkg"));
                var runtimeSubDir = DirTree.CreateFromDirectory(kreRoot)
                    .WithFileContents(kreName + ".nupkg.sha512", kreNupkgSHA)
                    .RemoveFile("[Content_Types].xml")
                    .RemoveFile(Path.Combine("_rels", ".rels"))
                    .RemoveSubDir("package");

                var batchFileBinPath = string.Format(@"%~dp0approot\packages\{0}\bin\", kreName);
                var bashScriptBinPath = string.Format("$DIR/approot/packages/{0}/bin/", kreName);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""commands"": {
    ""run"": ""run server.urls=http://localhost:5003"",
    ""kestrel"": ""Microsoft.AspNet.Hosting --server Kestrel --server.urls http://localhost:5004""
  },
  ""frameworks"": {
    ""aspnet50"": { },
    ""aspnetcore50"": { }
  }
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""dependencies"": {},
  ""packages"": ""packages""
}")
                    .WithFileContents("run.cmd", BatchFileTemplate, batchFileBinPath, testEnv.ProjectName, "run")
                    .WithFileContents("kestrel.cmd", BatchFileTemplate, batchFileBinPath, testEnv.ProjectName, "kestrel")
                    .WithFileContents("run",
                        BashScriptTemplate, testEnv.ProjectName, bashScriptBinPath, "run")
                    .WithFileContents("kestrel",
                        BashScriptTemplate, testEnv.ProjectName, bashScriptBinPath, "kestrel")
                    .WithSubDir(Path.Combine("approot", "packages", kreName), runtimeSubDir);

                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }
    }
}
