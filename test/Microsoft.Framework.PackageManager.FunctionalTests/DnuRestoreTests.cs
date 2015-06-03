// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.CommonTestUtils;
using Microsoft.Framework.PackageManager.FunctionalTests;
using Xunit;

namespace Microsoft.Framework.PackageManager
{
    [Collection(nameof(PackageManagerFunctionalTestCollection))]
    public class DnuRestoreTests
    {
        private readonly PackageManagerFunctionalTestFixture _fixture;

        public DnuRestoreTests(PackageManagerFunctionalTestFixture fixture)
        {
            _fixture = fixture;
        }

        public static IEnumerable<object[]> RuntimeComponents
        {
            get
            {
                return TestUtils.GetRuntimeComponentsCombinations();
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuRestore_ExecutesScripts(string flavor, string os, string architecture)
        {
            bool isWindows = TestUtils.CurrentRuntimeEnvironment.OperatingSystem == "Windows";
            var environment = new Dictionary<string, string>
            {
                { "DNX_TRACE", "0" },
            };

            var expectedPreContent =
@"""one""
""two""
"">three""
""four""
";
            var expectedPostContent =
@"""five""
""six""
""argument seven""
""argument eight""
";

            string projectJsonContent;
            string scriptContent;
            string scriptName;
            if (isWindows)
            {
                projectJsonContent =
@"{
  ""frameworks"": {
    ""dnx451"": { }
  },
  ""scripts"": {
    ""prerestore"": [
      ""script.cmd one two > pre"",
      ""script.cmd ^>three >> pre && script.cmd ^ four >> pre""
    ],
    ""postrestore"": [
      ""\""%project:Directory%/script.cmd\"" five six > post"",
      ""\""%project:Directory%/script.cmd\"" \""argument seven\"" \""argument eight\"" >> post""
    ]
  }
}";
                scriptContent =
@"@echo off

:argumentStart
if ""%~1""=="""" goto argumentEnd
echo ""%~1""
shift
goto argumentStart
:argumentEnd";
                scriptName = "script.cmd";
            }
            else
            {
                projectJsonContent =
@"{
  ""frameworks"": {
    ""dnx451"": { }
  },
  ""scripts"": {
    ""prerestore"": [
      ""script one two > pre"",
      ""script.sh \\>three >> pre; ./script.sh four >> pre""
    ],
    ""postrestore"": [
      ""\""%project:Directory%/script\"" five six > post"",
      ""\""%project:Directory%/script.sh\"" \""argument seven\"" \""argument eight\"" >> post""
    ]
  }
}";
                scriptContent =
@"#!/bin/bash --restricted
set -o errexit

for arg in ""$@""; do
  printf ""\""%s\""\n"" ""$arg""
done";
                scriptName = "script.sh";
            }

            var projectStructure =
$@"{{
  '.': ['project.json', '{ scriptName }']
}}";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var testEnv = new DnuTestEnvironment(runtimeHomePath, projectName: "Project Name"))
            {
                var projectPath = testEnv.ProjectPath;
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", projectJsonContent)
                    .WithFileContents(scriptName, scriptContent)
                    .WriteTo(projectPath);
                FileOperationUtils.MarkExecutable(Path.Combine(projectPath, scriptName));

                string output;
                string error;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "restore",
                    arguments: null,
                    stdOut: out output,
                    stdErr: out error,
                    environment: environment,
                    workingDir: projectPath);

                Assert.Equal(0, exitCode);
                Assert.Empty(error);
                Assert.Contains("Executing script 'prerestore' in project.json", output);
                Assert.Contains("Executing script 'postrestore' in project.json", output);

                var preContent = File.ReadAllText(Path.Combine(projectPath, "pre"));
                Assert.Equal(expectedPreContent, preContent);
                var postContent = File.ReadAllText(Path.Combine(projectPath, "post"));
                Assert.Equal(expectedPostContent, postContent);
            }
        }
    }
}
