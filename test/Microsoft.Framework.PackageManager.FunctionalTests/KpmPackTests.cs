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
        private readonly string _outputDirName = "PackOutput";

        private static readonly string BatchFileTemplate = @"
@""{0}dotnet.exe"" --appbase ""%~dp0approot\src\{1}"" Microsoft.Framework.ApplicationHost {2} %*
";

        private static readonly string BashScriptTemplate = @"#!/bin/bash

SOURCE=""${{BASH_SOURCE[0]}}""
while [ -h ""$SOURCE"" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""
  SOURCE=""$(readlink ""$SOURCE"")""
  [[ $SOURCE != /* ]] && SOURCE=""$DIR/$SOURCE"" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""

export SET DOTNET_APPBASE=""$DIR/approot/src/{0}""

exec ""{1}dotnet"" --appbase ""$DOTNET_APPBASE"" Microsoft.Framework.ApplicationHost {2} ""$@""".Replace("\r\n", "\n");

        public static IEnumerable<object[]> DotnetHomeDirs
        {
            get
            {
                foreach (var path in TestUtils.GetDotnetHomeDirs())
                {
                    yield return new[] { path };
                }
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KpmPackWebApp_RootAsPublicFolder(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": ""**.bconfig"",
  ""webroot"": ""to_be_overridden""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --wwwroot . --wwwroot-out wwwroot",
                        testEnv.PackOutputDirPath),
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
    <add key=""packages-path"" value=""..\approot\packages"" />
    <add key=""dotnet-version"" value="""" />
    <add key=""dotnet-clr"" value="""" />
    <add key=""dotnet-app-base"" value=""..\approot\src\{0}"" />
  </appSettings>
</configuration>", testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KpmPackWebApp_SubfolderAsPublicFolder(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": ""**.useless"",
  ""webroot"": ""public""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --wwwroot-out wwwroot",
                        testEnv.PackOutputDirPath),
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
    <add key=""packages-path"" value=""..\approot\packages"" />
    <add key=""dotnet-version"" value="""" />
    <add key=""dotnet-clr"" value="""" />
    <add key=""dotnet-app-base"" value=""..\approot\src\{0}"" />
  </appSettings>
</configuration>", testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KpmPackConsoleApp(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""packExclude"": ""Data/Backup/**""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
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
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void FoldersAsFilePatternsAutoGlob(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
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
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
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
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void WildcardMatchingFacts(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
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
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
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
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void CorrectlyExcludeFoldersStartingWithDots(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
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
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void VerifyDefaultPackExcludePatterns(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
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
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KpmPackWebApp_AppendToExistingWebConfig(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""webroot"": ""public""
}")
                    .WithFileContents(Path.Combine("public", "web.config"), webConfigContents)
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --wwwroot public --wwwroot-out wwwroot",
                        testEnv.PackOutputDirPath),
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
    <add key=""packages-path"" value=""..\approot\packages"" />
    <add key=""dotnet-version"" value="""" />
    <add key=""dotnet-clr"" value="""" />
    <add key=""dotnet-app-base"" value=""..\approot\src\{0}"" />
  </appSettings>
</configuration>", testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void KpmPackWebApp_UpdateExistingWebConfig(DisposableDir dotnetHomeDir)
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
    <add key=""packages-path"" value=""OLD_VALUE"" />
    <add key=""dotnet-version"" value=""OLD_VALUE"" />
    <add key=""dotnet-clr"" value=""OLD_VALUE"" />
    <add key=""dotnet-app-base"" value=""OLD_VALUE"" />
  </appSettings>
</configuration>";

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""webroot"": ""../../../wwwroot""
}")
                    .WithFileContents(Path.Combine("public", "web.config"), webConfigContents)
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --wwwroot public --wwwroot-out wwwroot",
                        testEnv.PackOutputDirPath),
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
    <add key=""packages-path"" value=""..\approot\packages"" />
    <add key=""dotnet-version"" value="""" />
    <add key=""dotnet-clr"" value="""" />
    <add key=""dotnet-app-base"" value=""..\approot\src\{0}"" />
  </appSettings>
</configuration>", testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void GenerateBatchFilesAndBashScriptsWithoutPackedRuntime(DisposableDir dotnetHomeDir)
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

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
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
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0}",
                        testEnv.PackOutputDirPath),
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

                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("DotnetHomeDirs")]
        public void GenerateBatchFilesAndBashScriptsWithPackedRuntime(DisposableDir dotnetHomeDir)
        {
            // Each runtime home only contains one runtime package, which is the one we are currently testing against
            var runtimeRoot = Directory.EnumerateDirectories(Path.Combine(dotnetHomeDir, "runtimes"), "dotnet-*").First();
            var runtimeName = new DirectoryInfo(runtimeRoot).Name;

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
      'RUNTIME_PACKAGE_NAME': {}
    }
  }
}".Replace("PROJECT_NAME", _projectName).Replace("RUNTIME_PACKAGE_NAME", runtimeName);

            using (var testEnv = new KpmTestEnvironment(dotnetHomeDir, _projectName, _outputDirName))
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
                    { "DOTNET_PACKAGES", Path.Combine(testEnv.ProjectPath, "packages") },
                    { "DOTNET_HOME", dotnetHomeDir },
                    { "DOTNET_TRACE", "1" }
                };

                var exitCode = KpmTestUtils.ExecKpm(
                    dotnetHomeDir,
                    subcommand: "pack",
                    arguments: string.Format("--out {0} --runtime {1}",
                        testEnv.PackOutputDirPath, runtimeName),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var runtimeNupkgSHA = TestUtils.ComputeSHA(Path.Combine(runtimeRoot, runtimeName + ".nupkg"));
                var runtimeSubDir = DirTree.CreateFromDirectory(runtimeRoot)
                    .WithFileContents(runtimeName + ".nupkg.sha512", runtimeNupkgSHA)
                    .RemoveFile("[Content_Types].xml")
                    .RemoveFile(Path.Combine("_rels", ".rels"))
                    .RemoveFile(Path.Combine("bin", "lib", "Microsoft.Framework.PackageManager",
                        "bin", "profile", "startup.prof"))
                    .RemoveSubDir("package");

                var batchFileBinPath = string.Format(@"%~dp0approot\packages\{0}\bin\", runtimeName);
                var bashScriptBinPath = string.Format("$DIR/approot/packages/{0}/bin/", runtimeName);

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
                    .WithSubDir(Path.Combine("approot", "packages", runtimeName), runtimeSubDir);

                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.PackOutputDirPath,
                    compareFileContents: true));
            }
        }
    }
}
