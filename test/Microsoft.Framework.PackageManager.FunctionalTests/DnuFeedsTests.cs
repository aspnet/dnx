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
    public class DnuFeedsTests
    {
        private readonly PackageManagerFunctionalTestFixture _fixture;

        public DnuFeedsTests(PackageManagerFunctionalTestFixture fixture)
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
        public void DnuFeeds_NoSources(string flavor, string os, string architecture)
        {
            var environment = new Dictionary<string, string>
            {
                { "DNX_TRACE", "0" },
            };

            var rootConfig =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear /> <!-- Remove the effects of any machine-level config -->
  </packageSources>
</configuration>";

            var projectStructure =
@"{
    'root': {
        'NuGet.Config': """",
    }
}";

            var expectedContent = @"Registered Sources:
1.  https://www.nuget.org/api/v2/ https://www.nuget.org/api/v2/
";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var testEnv = new DnuTestEnvironment(runtimeHomePath, projectName: "Project Name"))
            {
                var projectPath = testEnv.ProjectPath;
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("root/NuGet.Config", rootConfig)
                    .WriteTo(projectPath);

                string output;
                string error;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "feeds",
                    arguments: "list root",
                    stdOut: out output,
                    stdErr: out error,
                    environment: environment,
                    workingDir: projectPath);

                Assert.Equal(0, exitCode);
                Assert.Empty(error);
                Assert.Equal(expectedContent, output);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void DnuFeeds_ListsAllSources(string flavor, string os, string architecture)
        {
            var environment = new Dictionary<string, string>
            {
                { "DNX_TRACE", "0" },
            };

            var rootConfig =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear /> <!-- Remove the effects of any machine-level config -->
    <add key=""Source1"" value=""https://source1"" />
    <add key=""Source2"" value=""https://source2"" />
  </packageSources>
</configuration>";

            var subConfig =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""Source3"" value=""https://source3"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""Source1"" value=""https://source1"" />
  </disabledPackageSources>
</configuration>";

            var projectStructure =
@"{
    'root': {
        'NuGet.Config': """",
        'sub': {
            'NuGet.Config': """"
        }
    }
}";

            var expectedContent = @"Registered Sources:
1.  Source1 https://source1 [Disabled]
2.  Source2 https://source2
3.  Source3 https://source3
4.  https://www.nuget.org/api/v2/ https://www.nuget.org/api/v2/ [Disabled]
";
            var runtimeHomePath = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var testEnv = new DnuTestEnvironment(runtimeHomePath, projectName: "Project Name"))
            {
                var projectPath = testEnv.ProjectPath;
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("root/NuGet.Config", rootConfig)
                    .WithFileContents("root/sub/NuGet.Config", subConfig)
                    .WriteTo(projectPath);

                string output;
                string error;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomePath,
                    subcommand: "feeds",
                    arguments: "list root/sub",
                    stdOut: out output,
                    stdErr: out error,
                    environment: environment,
                    workingDir: projectPath);

                Assert.Equal(0, exitCode);
                Assert.Empty(error);
                Assert.Equal(expectedContent, output);
            }
        }
    }
}
