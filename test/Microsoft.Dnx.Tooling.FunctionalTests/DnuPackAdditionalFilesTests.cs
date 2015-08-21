using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Xunit;

namespace Microsoft.Dnx.Tooling.FunctionalTests
{
    public class DnuPackAdditionalFilesTests
    {
        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                return TestUtils.GetRuntimeComponentsCombinations();
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_SimpleGlobs(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools/"": ""packageTools/**/*.ps1""
    }   
}";

            var expectedOutput = new[]
            {
                "ProjectName.nuspec",
                "lib/dnx451/ProjectName.dll",
                "lib/dnx451/ProjectName.xml",
                "tools/install.ps1",
                "tools/sub/support.ps1"
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_FileToFile(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools/install.ps1"": ""packageTools/install.ps1"",
        ""tools/different/support.ps1"": ""packageTools/sub/support.ps1""
    }   
}";

            var expectedOutput = new[]
            {
                "ProjectName.nuspec",
                "lib/dnx451/ProjectName.dll",
                "lib/dnx451/ProjectName.xml",
                "tools/install.ps1",
                "tools/different/support.ps1"
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_SingleFileToDirectory(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools/"": ""packageTools/install.ps1""
    }   
}";

            var expectedOutput = new[]
            {
                "ProjectName.nuspec",
                "lib/dnx451/ProjectName.dll",
                "lib/dnx451/ProjectName.xml",
                "tools/install.ps1"
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_FileArray_RootFiles(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json"", ""root.txt""]
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""/"" : ""root.txt""
    }   
}";

            var expectedOutput = new[]
            {
                "ProjectName.nuspec",
                "lib/dnx451/ProjectName.dll",
                "lib/dnx451/ProjectName.xml",
                "root.txt"
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_FileArray_DotFiles(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json"", "".someconfigfile""]
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        "".someconfigfile"" : "".someconfigfile""
    }   
}";

            var expectedOutput = new[]
            {
                "ProjectName.nuspec",
                "lib/dnx451/ProjectName.dll",
                "lib/dnx451/ProjectName.xml",
                ".someconfigfile"
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_FileArray(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools/"": [ ""packageTools/install.ps1"", ""packageTools/sub/support.ps1"" ]
    }   
}";

            var expectedOutput = new[]
            {
                "ProjectName.nuspec",
                "lib/dnx451/ProjectName.dll",
                "lib/dnx451/ProjectName.xml",
                "tools/install.ps1",
                "tools/support.ps1"
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_MissingFile(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools/"": ""nope.ps1""
    }   
}";

            var expectedOutput = new[]
            {
                "ProjectName.nuspec",
                "lib/dnx451/ProjectName.dll",
                "lib/dnx451/ProjectName.xml"
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput, shouldFail: false);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_ArityMismatch_Glob(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools"": ""packageTools/**/*.ps1""
    }   
}";

            var expectedOutput = new[]
            {
                "PROJECTJSONPATH(5,18): error: Invalid 'packInclude' section. The target 'tools' refers to a single file, but the pattern \"packageTools/**/*.ps1\" produces multiple files. To mark the target as a directory, suffix it with '/'."
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput, shouldFail: true);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_ArityMismatch_Array(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools"": [ ""packageTools/install.ps1"", ""packageTools/sub/support.ps1"" ]
    }   
}";

            var expectedOutput = new[]
            {
                "PROJECTJSONPATH(5,18): error: Invalid 'packInclude' section. The target 'tools' refers to a single file, but the pattern [\"packageTools/install.ps1\",\"packageTools/sub/support.ps1\"] produces multiple files. To mark the target as a directory, suffix it with '/'."
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput, shouldFail: true);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_PathTraversal(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools/../"": [ ""packageTools/install.ps1"", ""packageTools/sub/support.ps1"" ]
    }   
}";

            var expectedOutput = new[]
            {
                "PROJECTJSONPATH(5,22): error: Invalid 'packInclude' section. The target 'tools/../' contains path-traversal characters ('.' or '..'). These characters are not permitted in target paths."
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput, shouldFail: true);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_PathTraversal_EmptyTarget(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        """": ""packageTools/install.ps1""
    }   
}";

            var expectedOutput = new[]
            {
                "PROJECTJSONPATH(5,13): error: Invalid 'packInclude' section. The target '' is invalid, targets must either be a file name or a directory suffixed with '/'. The root directory of the package can be specified by using a single '/' character."
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput, shouldFail: true);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuPack_AddFilesToNupkg_PathTraversal_SingleDot(string flavor, string os, string architecture)
        {
            const string dirTree = @"{
    ""."": [""project.json""],
    ""packageTools"": {
        ""."": [""install.ps1""],
        ""sub"": [""support.ps1""]
    }
}";

            const string projectJson = @"{
    ""version"": ""1.0.0"",
    ""frameworks"": { ""dnx451"": {} },
    ""packInclude"": {
        ""tools/./"": [ ""packageTools/install.ps1"", ""packageTools/sub/support.ps1"" ]
    }   
}";

            var expectedOutput = new[]
            {
                "PROJECTJSONPATH(5,21): error: Invalid 'packInclude' section. The target 'tools/./' contains path-traversal characters ('.' or '..'). These characters are not permitted in target paths."
            };

            RunAdditionalFilesTest(flavor, os, architecture, dirTree, projectJson, expectedOutput, shouldFail: true);
        }

        private void RunAdditionalFilesTest(string flavor, string os, string architecture, string dirTree, string projectJson, string[] expectedOutput, bool shouldFail = false)
        {
            var runtimeHomeDir = TestUtils.GetRuntimeHomeDir(flavor, os, architecture);
            using (var testEnv = new DnuTestEnvironment(runtimeHomeDir))
            {
                int exitCode;
                string stdOut;
                string stdErr;

                DirTree.CreateFromJson(dirTree)
                    .WithFileContents("project.json", projectJson)
                    .WriteTo(testEnv.ProjectPath);

                DnuTestUtils.ExecDnu(runtimeHomeDir, "restore", "", workingDir: testEnv.RootDir);
                exitCode = DnuTestUtils.ExecDnu(runtimeHomeDir, "pack", $"--out {testEnv.PublishOutputDirPath}", out stdOut, out stdErr, workingDir: testEnv.ProjectPath);

                var packageOutputPath = Path.Combine(testEnv.PublishOutputDirPath, "Debug", $"{testEnv.ProjectName}.1.0.0.nupkg");

                // Check it
                if (shouldFail)
                {
                    Assert.NotEqual(0, exitCode);
                    foreach (var message in expectedOutput)
                    {
                        Assert.Contains(
                            message.Replace("PROJECTJSONPATH", Path.Combine(testEnv.ProjectPath, "project.json")),
                            stdErr.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
                    }
                    Assert.False(File.Exists(packageOutputPath));
                }
                else
                {
                    Assert.True(File.Exists(packageOutputPath));

                    string[] entries;
                    using (var archive = ZipFile.OpenRead(packageOutputPath))
                    {
                        entries = archive.Entries.Select(e => e.FullName).Where(IsNotOpcMetadata).ToArray();
                    }

                    Assert.Equal(0, exitCode);
                    Assert.Equal(expectedOutput, entries);
                }
            }
        }

        private static readonly HashSet<string> OpcMetadataPaths = new HashSet<string>()
        {
            "_rels/.rels",
            "[Content_Types].xml"
        };

        private bool IsNotOpcMetadata(string arg)
        {
            return !OpcMetadataPaths.Contains(arg);
        }
    }
}
