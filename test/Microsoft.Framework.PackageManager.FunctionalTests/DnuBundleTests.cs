// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.FunctionalTestUtils;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.DependencyManagement;
using Xunit;

namespace Microsoft.Framework.PackageManager
{
    public class DnuBundleTests
    {
        private readonly string _projectName = "TestProject";
        private readonly string _outputDirName = "BundleOutput";

        private static readonly string BatchFileTemplate = @"
@""{0}{1}.exe"" --appbase ""%~dp0approot\src\{2}"" Microsoft.Framework.ApplicationHost {3} %*
";

        private static readonly string BashScriptTemplate = @"#!/bin/bash

SOURCE=""${{BASH_SOURCE[0]}}""
while [ -h ""$SOURCE"" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""
  SOURCE=""$(readlink ""$SOURCE"")""
  [[ $SOURCE != /* ]] && SOURCE=""$DIR/$SOURCE"" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""

export SET {0}=""$DIR/approot/src/{1}""

exec ""{2}{3}"" --appbase ""${0}"" Microsoft.Framework.ApplicationHost {4} ""$@""".Replace("\r\n", "\n");

        private static readonly string BasicLockFile = @"{
  ""locked"": false,
  ""version"": LOCKFILEFORMAT_VERSION,
  ""projectFileDependencyGroups"": {
    """": []
  },
  ""libraries"": {}
}".Replace("LOCKFILEFORMAT_VERSION", LockFileFormat.Version.ToString());

        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                return TestUtils.GetRuntimeComponentsCombinations();
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuBundleWebApp_RootAsPublicFolder(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json', 'Config.json', 'Program.cs'],
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

            var outputWebConfigTemplate = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""{0}"" value="""" />
    <add key=""{1}"" value=""..\approot\packages"" />
    <add key=""{2}"" value="""" />
    <add key=""{3}"" value="""" />
    <add key=""{4}"" value=""..\approot\src\{{0}}"" />
  </appSettings>
</configuration>", Constants.WebConfigBootstrapperVersion,
                Constants.WebConfigRuntimePath,
                Constants.WebConfigRuntimeVersion,
                Constants.WebConfigRuntimeFlavor,
                Constants.WebConfigRuntimeAppBase);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""bundleExclude"": ""**.bconfig"",
  ""webroot"": ""to_be_overridden""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0} --wwwroot . --wwwroot-out wwwroot",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""bundleExclude"": ""**.bconfig"",
  ""webroot"": ""../../../wwwroot""
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("wwwroot", "project.json"), @"{
  ""bundleExclude"": ""**.bconfig"",
  ""webroot"": ""to_be_overridden""
}")
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}")
                    .WithFileContents(Path.Combine("wwwroot", "web.config"), outputWebConfigTemplate, testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuBundleWebApp_SubfolderAsPublicFolder(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json', 'Config.json', 'Program.cs'],
          'Views': {
            'Home': ['index.cshtml'],
            'Shared': ['_Layout.cshtml']
        },
        'Controllers': ['HomeController.cs'],
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);
            var outputWebConfigTemplate = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""{0}"" value="""" />
    <add key=""{1}"" value=""..\approot\packages"" />
    <add key=""{2}"" value="""" />
    <add key=""{3}"" value="""" />
    <add key=""{4}"" value=""..\approot\src\{{0}}"" />
  </appSettings>
</configuration>", Constants.WebConfigBootstrapperVersion,
                Constants.WebConfigRuntimePath,
                Constants.WebConfigRuntimeVersion,
                Constants.WebConfigRuntimeFlavor,
                Constants.WebConfigRuntimeAppBase);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""bundleExclude"": ""**.useless"",
  ""webroot"": ""public""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0} --wwwroot-out wwwroot",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""bundleExclude"": ""**.useless"",
  ""webroot"": ""../../../wwwroot""
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}")
                    .WithFileContents(Path.Combine("wwwroot", "web.config"), outputWebConfigTemplate, testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuBundleConsoleApp(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json', 'Config.json', 'Program.cs'],
          'Data': {
            'Input': ['data1.dat', 'data2.dat']
          }
        }
      }
    }
  }".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""bundleExclude"": ""Data/Backup/**""
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""bundleExclude"": ""Data/Backup/**""
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void FoldersAsFilePatternsAutoGlob(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json'],
        'MixFolder': {
          'UsefulSub': ['useful.txt', 'useful.css', 'file_without_extension']
        }
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""bundleExclude"": [
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
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""bundleExclude"": [
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
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void WildcardMatchingFacts(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json'],
        'MixFolder1': {
          '.': ['uselessfile1.txt', 'uselessfile2'],
          'UsefulSub': ['useful.txt', 'useful']
        },
        'MixFolder2': {
          '.': ['uselessfile1.txt', 'uselessfile2'],
          'UsefulSub': ['useful.txt', 'useful']
        }
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""bundleExclude"": [
    ""UselessFolder1\\**"",
    ""UselessFolder2/**/*"",
    ""UselessFolder3\\**/*.*""
  ]
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""bundleExclude"": [
    ""UselessFolder1\\**"",
    ""UselessFolder2/**/*"",
    ""UselessFolder3\\**/*.*""
  ]
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void CorrectlyExcludeFoldersStartingWithDots(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json', 'File', '.FileStartingWithDot', 'File.Having.Dots'],
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

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void VerifyDefaultBundleExcludePatterns(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json', 'File', '.FileStartingWithDot'],
        'Folder': ['File', '.FileStartingWithDot']
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuBundleWebApp_CopyExistingWebConfig(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
      'PROJECT_NAME': ['project.json', 'project.lock.json']
    }
  }
}".Replace("PROJECT_NAME", _projectName);
            var originalWebConfigContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""non-related-value"" />
  </nonRelatedElement>
</configuration>";
            var outputWebConfigTemplate = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""non-related-value"" />
  </nonRelatedElement>
  <appSettings>
    <add key=""{0}"" value="""" />
    <add key=""{1}"" value=""..\approot\packages"" />
    <add key=""{2}"" value="""" />
    <add key=""{3}"" value="""" />
    <add key=""{4}"" value=""..\approot\src\{{0}}"" />
  </appSettings>
</configuration>", Constants.WebConfigBootstrapperVersion,
                Constants.WebConfigRuntimePath,
                Constants.WebConfigRuntimeVersion,
                Constants.WebConfigRuntimeFlavor,
                Constants.WebConfigRuntimeAppBase);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""webroot"": ""public""
}")
                    .WithFileContents(Path.Combine("public", "web.config"), originalWebConfigContents)
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
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
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}")
                    .WithFileContents(Path.Combine("wwwroot", "web.config"), outputWebConfigTemplate, testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void DnuBundleWebApp_UpdateExistingWebConfig(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
      'PROJECT_NAME': ['project.json', 'project.lock.json']
    }
  }
}".Replace("PROJECT_NAME", _projectName);
            var originalWebConfigContents = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""OLD_VALUE"" />
  </nonRelatedElement>
  <appSettings>
    <add key=""non-related-key"" value=""OLD_VALUE"" />
    <add key=""{0}"" value=""OLD_VALUE"" />
    <add key=""{1}"" value=""OLD_VALUE"" />
    <add key=""{2}"" value=""OLD_VALUE"" />
    <add key=""{3}"" value=""OLD_VALUE"" />
    <add key=""{4}"" value=""OLD_VALUE"" />
  </appSettings>
</configuration>", Constants.WebConfigBootstrapperVersion,
                Constants.WebConfigRuntimePath,
                Constants.WebConfigRuntimeVersion,
                Constants.WebConfigRuntimeFlavor,
                Constants.WebConfigRuntimeAppBase);

            var outputWebConfigContents = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <nonRelatedElement>
    <add key=""non-related-key"" value=""OLD_VALUE"" />
  </nonRelatedElement>
  <appSettings>
    <add key=""non-related-key"" value=""OLD_VALUE"" />
    <add key=""{0}"" value="""" />
    <add key=""{1}"" value=""..\approot\packages"" />
    <add key=""{2}"" value="""" />
    <add key=""{3}"" value="""" />
    <add key=""{4}"" value=""..\approot\src\{{0}}"" />
  </appSettings>
</configuration>", Constants.WebConfigBootstrapperVersion,
                Constants.WebConfigRuntimePath,
                Constants.WebConfigRuntimeVersion,
                Constants.WebConfigRuntimeFlavor,
                Constants.WebConfigRuntimeAppBase);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""webroot"": ""../../../wwwroot""
}")
                    .WithFileContents(Path.Combine("public", "web.config"), originalWebConfigContents)
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
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
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}")
                    .WithFileContents(Path.Combine("wwwroot", "web.config"), outputWebConfigContents, testEnv.ProjectName);
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void GenerateBatchFilesAndBashScriptsWithoutBundledRuntime(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json']
      }
    }
  }
}".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""commands"": {
    ""run"": ""run server.urls=http://localhost:5003"",
    ""kestrel"": ""Microsoft.AspNet.Hosting --server Kestrel --server.urls http://localhost:5004""
  },
  ""frameworks"": {
    ""dnx451"": { },
    ""dnxcore50"": { }
  }
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
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
    ""dnx451"": { },
    ""dnxcore50"": { }
  }
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"), @"{
  ""locked"": false,
  ""version"": LOCKFILEFORMAT_VERSION,
  ""projectFileDependencyGroups"": {
    """": [],
    ""DNX,Version=v4.5.1"": [],
    ""DNXCore,Version=v5.0"": []
  },
  ""libraries"": {}
}".Replace("LOCKFILEFORMAT_VERSION", LockFileFormat.Version.ToString()))
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}")
                    .WithFileContents("run.cmd", BatchFileTemplate, string.Empty, Constants.BootstrapperExeName, testEnv.ProjectName, "run")
                    .WithFileContents("kestrel.cmd", BatchFileTemplate, string.Empty, Constants.BootstrapperExeName, testEnv.ProjectName, "kestrel")
                    .WithFileContents("run",
                        BashScriptTemplate, EnvironmentNames.AppBase, testEnv.ProjectName, string.Empty, Constants.BootstrapperExeName, "run")
                    .WithFileContents("kestrel",
                        BashScriptTemplate, EnvironmentNames.AppBase, testEnv.ProjectName, string.Empty, Constants.BootstrapperExeName, "kestrel");

                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [MemberData("RuntimeComponents")]
        public void GenerateBatchFilesAndBashScriptsWithBundledRuntime(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

            // Each runtime home only contains one runtime package, which is the one we are currently testing against
            var runtimeRoot = Directory.EnumerateDirectories(Path.Combine(runtimeHomeDir, "runtimes"), Constants.RuntimeNamePrefix + "*").First();
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
        '.': ['project.json', 'project.lock.json']
      }
    },
    'packages': {
      'RUNTIME_PACKAGE_NAME': {}
    }
  }
}".Replace("PROJECT_NAME", _projectName).Replace("RUNTIME_PACKAGE_NAME", runtimeName);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""commands"": {
    ""run"": ""run server.urls=http://localhost:5003"",
    ""kestrel"": ""Microsoft.AspNet.Hosting --server Kestrel --server.urls http://localhost:5004""
  },
  ""frameworks"": {
    ""dnx451"": { },
    ""dnxcore50"": { }
  }
}")
                    .WriteTo(testEnv.ProjectPath);

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") },
                    { EnvironmentNames.Home, runtimeHomeDir },
                    { EnvironmentNames.Trace, "1" }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0} --runtime {1}",
                        testEnv.BundleOutputDirPath, runtimeName),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var runtimeSubDir = DirTree.CreateFromDirectory(runtimeRoot)
                    .RemoveFile(Path.Combine("bin", "lib", "Microsoft.Framework.PackageManager",
                        "bin", "profile", "startup.prof"));

                var batchFileBinPath = string.Format(@"%~dp0approot\packages\{0}\bin\", runtimeName);
                var bashScriptBinPath = string.Format("$DIR/approot/packages/{0}/bin/", runtimeName);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""commands"": {
    ""run"": ""run server.urls=http://localhost:5003"",
    ""kestrel"": ""Microsoft.AspNet.Hosting --server Kestrel --server.urls http://localhost:5004""
  },
  ""frameworks"": {
    ""dnx451"": { },
    ""dnxcore50"": { }
  }
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"), @"{
  ""locked"": false,
  ""version"": LOCKFILEFORMAT_VERSION,
  ""projectFileDependencyGroups"": {
    """": [],
    ""DNX,Version=v4.5.1"": [],
    ""DNXCore,Version=v5.0"": []
  },
  ""libraries"": {}
}".Replace("LOCKFILEFORMAT_VERSION", LockFileFormat.Version.ToString()))
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}")
                    .WithFileContents("run.cmd", BatchFileTemplate, batchFileBinPath, Constants.BootstrapperExeName, testEnv.ProjectName, "run")
                    .WithFileContents("kestrel.cmd", BatchFileTemplate, batchFileBinPath, Constants.BootstrapperExeName, testEnv.ProjectName, "kestrel")
                    .WithFileContents("run",
                        BashScriptTemplate, EnvironmentNames.AppBase, testEnv.ProjectName, bashScriptBinPath, Constants.BootstrapperExeName, "run")
                    .WithFileContents("kestrel",
                        BashScriptTemplate, EnvironmentNames.AppBase, testEnv.ProjectName, bashScriptBinPath, Constants.BootstrapperExeName, "kestrel")
                    .WithSubDir(Path.Combine("approot", "packages", runtimeName), runtimeSubDir);

                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        [Theory]
        [InlineData("clr", "win", "x86")]
        [InlineData("clr", "win", "x64")]
        public void BundleWithNoSourceOptionGeneratesLockFileOnClr(string flavor, string os, string architecture)
        {
            const string testApp = "NoDependencies";
            string expectedOutputStructure = @"{
  '.': ['hello', 'hello.cmd'],
  'approot': {
    'global.json': '',
    'packages': {
      'NoDependencies': {
        '1.0.0': {
          '.': ['NoDependencies.1.0.0.nupkg', 'NoDependencies.1.0.0.nupkg.sha512', 'NoDependencies.nuspec'],
          'app': ['hello', 'hello.cmd', 'project.json'],
          'root': ['project.json', 'LOCKFILE_NAME'],
          'lib': {
            'dnx451': ['NoDependencies.dll', 'NoDependencies.xml']
          }
        }
      }
    }
  }
}".Replace("LOCKFILE_NAME", LockFileFormat.LockFileName);
            string expectedLockFileContents = @"{
  ""locked"": false,
  ""version"": LOCKFILEFORMAT_VERSION,
  ""projectFileDependencyGroups"": {
    ""DNX,Version=v4.5.1"": [],
    """": [
      ""NoDependencies >= 1.0.0""
    ]
  },
  ""libraries"": {
    ""NoDependencies/1.0.0"": {
      ""serviceable"": false,
      ""sha"": ""NUPKG_SHA_VALUE"",
      ""frameworks"": {
        ""DNX,Version=v4.5.1"": {
          ""dependencies"": {},
          ""frameworkAssemblies"": [
            ""mscorlib"",
            ""System"",
            ""System.Core"",
            ""Microsoft.CSharp""
          ],
          ""runtimeAssemblies"": [
            ""lib/dnx451/NoDependencies.dll""
          ],
          ""compileAssemblies"": [
            ""lib/dnx451/NoDependencies.dll""
          ]
        }
      },
      ""files"": [
        ""NoDependencies.1.0.0.nupkg"",
        ""NoDependencies.1.0.0.nupkg.sha512"",
        ""NoDependencies.nuspec"",
        ""app/hello"",
        ""app/hello.cmd"",
        ""app/project.json"",
        ""lib/dnx451/NoDependencies.dll"",
        ""lib/dnx451/NoDependencies.xml"",
        ""root/project.json""
      ]
    }
  }
}".Replace("LOCKFILEFORMAT_VERSION", LockFileFormat.Version.ToString());

            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            using (var tempDir = TestUtils.CreateTempDir())
            {
                var bundleOutputPath = Path.Combine(tempDir, "output");
                var appPath = Path.Combine(tempDir, testApp);
                TestUtils.CopyFolder(TestUtils.GetXreTestAppPath(testApp), appPath);

                var lockFilePath = Path.Combine(appPath, LockFileFormat.LockFileName);
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--no-source --out {0}", bundleOutputPath),
                    environment: null,
                    workingDir: appPath);

                Assert.Equal(0, exitCode);

                Assert.True(DirTree.CreateFromJson(expectedOutputStructure)
                    .MatchDirectoryOnDisk(bundleOutputPath, compareFileContents: false));

                var outputLockFilePath = Path.Combine(bundleOutputPath,
                    "approot", "packages", testApp, "1.0.0", "root", LockFileFormat.LockFileName);
                var nupkgSha = File.ReadAllText(Path.Combine(bundleOutputPath,
                    "approot", "packages", testApp, "1.0.0", $"{testApp}.1.0.0.nupkg.sha512"));

                Assert.Equal(expectedLockFileContents.Replace("NUPKG_SHA_VALUE", nupkgSha),
                    File.ReadAllText(outputLockFilePath));
            }
        }

        [Theory]
        [InlineData("clr", "win", "x86")]
        [InlineData("clr", "win", "x64")]
        public void BundleWithNoSourceOptionUpdatesLockFileOnClr(string flavor, string os, string architecture)
        {
            const string testApp = "NoDependencies";
            string expectedOutputStructure = @"{
  '.': ['hello', 'hello.cmd'],
  'approot': {
    'global.json': '',
    'packages': {
      'NoDependencies': {
        '1.0.0': {
          '.': ['NoDependencies.1.0.0.nupkg', 'NoDependencies.1.0.0.nupkg.sha512', 'NoDependencies.nuspec'],
          'app': ['hello', 'hello.cmd', 'project.json'],
          'root': ['project.json', 'LOCKFILE_NAME'],
          'lib': {
            'dnx451': ['NoDependencies.dll', 'NoDependencies.xml']
          }
        }
      }
    }
  }
}".Replace("LOCKFILE_NAME", LockFileFormat.LockFileName);
            var expectedLockFileContents = @"{
  ""locked"": false,
  ""version"": LOCKFILEFORMAT_VERSION,
  ""projectFileDependencyGroups"": {
    ""DNX,Version=v4.5.1"": [],
    """": [
      ""NoDependencies >= 1.0.0""
    ]
  },
  ""libraries"": {
    ""NoDependencies/1.0.0"": {
      ""serviceable"": false,
      ""sha"": ""NUPKG_SHA_VALUE"",
      ""frameworks"": {
        ""DNX,Version=v4.5.1"": {
          ""dependencies"": {},
          ""frameworkAssemblies"": [
            ""mscorlib"",
            ""System"",
            ""System.Core"",
            ""Microsoft.CSharp""
          ],
          ""runtimeAssemblies"": [
            ""lib/dnx451/NoDependencies.dll""
          ],
          ""compileAssemblies"": [
            ""lib/dnx451/NoDependencies.dll""
          ]
        }
      },
      ""files"": [
        ""NoDependencies.1.0.0.nupkg"",
        ""NoDependencies.1.0.0.nupkg.sha512"",
        ""NoDependencies.nuspec"",
        ""app/hello"",
        ""app/hello.cmd"",
        ""app/project.json"",
        ""lib/dnx451/NoDependencies.dll"",
        ""lib/dnx451/NoDependencies.xml"",
        ""root/project.json"",
        ""root/LOCKFILE_NAME""
      ]
    }
  }
}".Replace("LOCKFILEFORMAT_VERSION", LockFileFormat.Version.ToString())
.Replace("LOCKFILE_NAME", LockFileFormat.LockFileName);

            using (var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture))
            using (var tempDir = TestUtils.CreateTempDir())
            {
                var bundleOutputPath = Path.Combine(tempDir, "output");
                var appPath = Path.Combine(tempDir, testApp);
                TestUtils.CopyFolder(TestUtils.GetXreTestAppPath(testApp), appPath);

                // Generate lockfile for the HelloWorld app
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "restore",
                    arguments: string.Empty,
                    environment: null,
                    workingDir: appPath);

                Assert.Equal(0, exitCode);

                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--no-source --out {0}", bundleOutputPath),
                    environment: null,
                    workingDir: appPath);

                Assert.Equal(0, exitCode);

                Assert.True(DirTree.CreateFromJson(expectedOutputStructure)
                    .MatchDirectoryOnDisk(bundleOutputPath, compareFileContents: false));

                var outputLockFilePath = Path.Combine(bundleOutputPath,
                    "approot", "packages", testApp, "1.0.0", "root", LockFileFormat.LockFileName);
                var nupkgSha = File.ReadAllText(Path.Combine(bundleOutputPath,
                    "approot", "packages", testApp, "1.0.0", $"{testApp}.1.0.0.nupkg.sha512"));

                Assert.Equal(expectedLockFileContents.Replace("NUPKG_SHA_VALUE", nupkgSha),
                    File.ReadAllText(outputLockFilePath));
            }
        }


        [Theory]
        [MemberData("RuntimeComponents")]
        public void BundleExcludeWithLongPath(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);

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
        '.': ['project.json', 'project.lock.json', 'Config.json', 'Program.cs'],
          'Data': {
            'Input': ['data1.dat', 'data2.dat']
          }
        }
      }
    }
  }".Replace("PROJECT_NAME", _projectName);

            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir, _projectName, _outputDirName))
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", @"{
  ""bundleExclude"": ""Data/Backup/**"",
  ""exclude"": ""node_modules""
}")
                    .WriteTo(testEnv.ProjectPath);

                BuildLongPath(Path.Combine(testEnv.ProjectPath, "node_modules"));

                var environment = new Dictionary<string, string>()
                {
                    { EnvironmentNames.Packages, Path.Combine(testEnv.ProjectPath, "packages") }
                };

                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "bundle",
                    arguments: string.Format("--out {0}",
                        testEnv.BundleOutputDirPath),
                    environment: environment,
                    workingDir: testEnv.ProjectPath);
                Assert.Equal(0, exitCode);

                var expectedOutputDir = DirTree.CreateFromJson(expectedOutputStructure)
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.json"), @"{
  ""bundleExclude"": ""Data/Backup/**"",
  ""exclude"": ""node_modules""
}")
                    .WithFileContents(Path.Combine("approot", "src", testEnv.ProjectName, "project.lock.json"),
                        BasicLockFile)
                    .WithFileContents(Path.Combine("approot", "global.json"), @"{
  ""packages"": ""packages""
}");
                Assert.True(expectedOutputDir.MatchDirectoryOnDisk(testEnv.BundleOutputDirPath,
                    compareFileContents: true));
            }
        }

        private string BuildLongPath(string baseDir)
        {
            const int maxPath = 248;
            var resultPath = baseDir;

            string randomFilename;
            string newpath;
            while (true)
            {
                randomFilename = Path.GetRandomFileName();
                newpath = string.Format("{0}{1}{2}", resultPath, Path.DirectorySeparatorChar, randomFilename);

                if (newpath.Length > maxPath)
                {
                    break;
                }
                else
                {
                    resultPath = newpath;
                }
            }

            var currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.CreateDirectory(resultPath);
                Directory.SetCurrentDirectory(resultPath);
                File.WriteAllText(randomFilename, "wow");
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }

            return resultPath;
        }
    }
}
