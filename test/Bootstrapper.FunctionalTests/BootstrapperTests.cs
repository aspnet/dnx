// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Tooling;
using Microsoft.Dnx.Runtime;
using Xunit;

namespace Bootstrapper.FunctionalTests
{
    [Collection("BootstrapperTestCollection")]
    public class BootstrapperTests
    {
        private readonly DnxRuntimeFixture _fixture;
        private const string ClrVersionTestProgram = @"using System;
using System.Reflection;
using System.Runtime.Versioning;

class Program
{
    public void Main(string[] args)
    {
        // This is super gross! Don't EVER use this is real code, just trying to figure out exactly
        // which CLR quirking mode we're in for testing purposes.
        var typ = typeof(string).Assembly.GetType(""System.Runtime.Versioning.BinaryCompatibility"");
        var method = typ.GetProperty(""AppWasBuiltForVersion"", BindingFlags.NonPublic | BindingFlags.Static);
        var val = method.GetValue(null, new object[0]);
        Console.WriteLine(val);
    }
}";

        private const string AppSettingTestProgram = @"using System;
using System.Configuration;

namespace ConfigTest
{
    public class Program
    {
        public void Main(string[] args)
        {
            Console.WriteLine($""TheSetting={ConfigurationManager.AppSettings[""TheSetting""]}"");
        }
    }
}";

        public BootstrapperTests(DnxRuntimeFixture fixture)
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

        public static IEnumerable<object[]> ClrRuntimeComponents
        {
            get
            {
                return TestUtils.GetClrRuntimeComponents();
            }
        }

        public static IEnumerable<object[]> CoreClrRuntimeComponents
        {
            get
            {
                return TestUtils.GetCoreClrRuntimeComponents();
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void BootstrapperReturnsNonZeroExitCodeWhenNoArgumentWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = TestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: string.Empty,
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.NotEqual(0, exitCode);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void BootstrapperReturnsZeroExitCodeWhenHelpOptionWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = TestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: "--help",
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.Equal(0, exitCode);
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void BootstrapperShowsVersionAndReturnsZeroExitCodeWhenVersionOptionWasGiven(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            string stdOut, stdErr;
            var exitCode = TestUtils.ExecBootstrapper(
                runtimeHomeDir,
                arguments: "--version",
                stdOut: out stdOut,
                stdErr: out stdErr);

            Assert.Equal(0, exitCode);
            Assert.Contains(TestUtils.GetRuntimeVersion(), stdOut);

        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void BootstrapperInvokesApplicationHostWithExplicitAppBase(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempSamplesDir = TestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            {
                var testAppPath = Path.Combine(tempSamplesDir, "HelloWorld");

                string stdOut, stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: $"--appbase {testAppPath} Microsoft.Dnx.ApplicationHost run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } });

                Assert.Equal(0, exitCode);
                Assert.Equal(@"Hello World!
Hello, code!
I
can
customize
the
default
command
", stdOut);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void BootstrapperInvokesApplicationHostWithNoAppbase(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempSamplesDir = TestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            {
                var testAppPath = Path.Combine(tempSamplesDir, "HelloWorld");

                string stdOut, stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: testAppPath);

                Assert.Equal(0, exitCode);
                Assert.Equal(@"Hello World!
Hello, code!
I
can
customize
the
default
command
", stdOut);
            }
        }


        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void BootstrapperInvokesApplicationHostWithInferredAppBase_ProjectFileAsArgument(string flavor, string os, string architecture)
        {
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempSamplesDir = TestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            {
                var testAppPath = Path.Combine(tempSamplesDir, "HelloWorld");
                var testAppProjectFile = Path.Combine(testAppPath, Project.ProjectFileName);

                string stdOut, stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: $"-p {testAppProjectFile} run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } });

                Assert.Equal(0, exitCode);
                Assert.Equal(@"Hello World!
Hello, code!
I
can
customize
the
default
command
", stdOut);
            }
        }

        [Theory]
        [MemberData(nameof(RuntimeComponents))]
        public void BootstrapperInvokesAssemblyWithInferredAppBaseAndLibPathOnClr(string flavor, string os, string architecture)
        {
            var outputFolder = flavor == "coreclr" ? "dnxcore50" : "dnx451";
            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);

            using (var tempSamplesDir = TestUtils.PrepareTemporarySamplesFolder(runtimeHomeDir))
            using (var tempDir = new DisposableDir())
            {
                var sampleAppRoot = Path.Combine(tempSamplesDir, "HelloWorld");

                string stdOut, stdErr;
                var exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "build",
                    arguments: string.Format("{0} --configuration=Release --out {1}", sampleAppRoot, tempDir),
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                if (exitCode != 0)
                {
                    Console.WriteLine(stdOut);
                    Console.WriteLine(stdErr);
                }

                exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: Path.Combine(tempDir, "Release", outputFolder, "HelloWorld.dll"),
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } });

                Assert.Equal(0, exitCode);
                Assert.Equal(@"Hello World!
Hello, code!
", stdOut);
            }
        }

        [ConditionalTheory]
        [MemberData(nameof(ClrRuntimeComponents))]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Linux)]
        public void BootstrapperLaunches451ClrIfDnx451IsHighestVersionInProject(string flavor, string os, string architecture)
        {
            const string projectStructure = @"{
    ""project.json"": {},
    ""project.lock.json"": {},
    ""Program.cs"": {}
}";

            const string projectJson = @"{
    ""dependencies"": {
    },
    ""frameworks"": {
        ""dnx451"": {
        }
    }
}";
            const string lockFile = @"{
  ""locked"": false,
  ""version"": 2,
  ""targets"": {
    ""DNX,Version=v4.5.1"": {}
  },
  ""libraries"": {},
  ""projectFileDependencyGroups"": {
    """": [],
    ""DNX,Version=v4.5.1"": []
  }
}";

            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", projectJson)
                    .WithFileContents("project.lock.json", lockFile)
                    .WithFileContents("Program.cs", ClrVersionTestProgram)
                    .WriteTo(tempDir);

                string stdOut;
                string stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "-p . run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: tempDir);
                Assert.Equal(0, exitCode);
                Assert.Equal("40501", stdOut.Trim());
            }
        }

        [ConditionalTheory]
        [MemberData(nameof(ClrRuntimeComponents))]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Linux)]
        public void BootstrapperLaunches452ClrIfDnx452IsHighestVersionInProject(string flavor, string os, string architecture)
        {
            const string projectStructure = @"{
    ""project.json"": {},
    ""project.lock.json"": {},
    ""Program.cs"": {}
}";

            const string projectJson = @"{
    ""dependencies"": {
    },
    ""frameworks"": {
        ""dnx452"": {},
        ""dnx451"": {}
    }
}";
            const string lockFile = @"{
  ""locked"": false,
  ""version"": 2,
  ""targets"": {
    ""DNX,Version=v4.5.1"": {},
    ""DNX,Version=v4.5.2"": {}
  },
  ""libraries"": {},
  ""projectFileDependencyGroups"": {
    """": [],
    ""DNX,Version=v4.5.1"": [],
    ""DNX,Version=v4.5.2"": []
  }
}";

            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", projectJson)
                    .WithFileContents("project.lock.json", lockFile)
                    .WithFileContents("Program.cs", ClrVersionTestProgram)
                    .WriteTo(tempDir);

                string stdOut;
                string stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "--project . run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: tempDir);
                Assert.Equal(0, exitCode);
                Assert.Equal("40502", stdOut.Trim());
            }
        }

        [ConditionalTheory]
        [MemberData(nameof(ClrRuntimeComponents))]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Linux)]
        public void BootstrapperLaunches46ClrIfDnx46IsHighestVersionInProject(string flavor, string os, string architecture)
        {
            const string projectStructure = @"{
    ""project.json"": {},
    ""project.lock.json"": {},
    ""Program.cs"": {}
}";

            const string projectJson = @"{
    ""dependencies"": {
    },
    ""frameworks"": {
        ""dnx46"": {
        },
        ""dnx451"": {
        }
    }
}";
            const string lockFile = @"{
  ""locked"": false,
  ""version"": 2,
  ""targets"": {
    ""DNX,Version=v4.6"": {},
    ""DNX,Version=v4.5.1"": {}
  },
  ""libraries"": {},
  ""projectFileDependencyGroups"": {
    """": [],
    ""DNX,Version=v4.6"": [],
    ""DNX,Version=v4.5.1"": []
  }
}";

            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", projectJson)
                    .WithFileContents("project.lock.json", lockFile)
                    .WithFileContents("Program.cs", ClrVersionTestProgram)
                    .WriteTo(tempDir);

                string stdOut;
                string stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: "-p . run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: tempDir);

                Assert.Equal(0, exitCode);
                Assert.Equal("40600", stdOut.Trim());
            }
        }

        [ConditionalTheory]
        [MemberData(nameof(ClrRuntimeComponents))]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Linux)]
        public void BootstrapperLaunchesRequestedFramworkVersionIfOptionProvided_Dnx451(string flavor, string os, string architecture)
        {
            BootstrapperLaunchesRequestedFramworkVersionIfOptionProvided(flavor, os, architecture, "dnx451", "40501");
        }

        [ConditionalTheory]
        [MemberData(nameof(ClrRuntimeComponents))]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Linux)]
        public void BootstrapperLaunchesRequestedFramworkVersionIfOptionProvided_Dnx452(string flavor, string os, string architecture)
        {
            BootstrapperLaunchesRequestedFramworkVersionIfOptionProvided(flavor, os, architecture, "dnx452", "40502");
        }

        [ConditionalTheory]
        [MemberData(nameof(ClrRuntimeComponents))]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Linux)]
        public void BootstrapperLaunchesRequestedFramworkVersionIfOptionProvided_Dnx46(string flavor, string os, string architecture)
        {
            BootstrapperLaunchesRequestedFramworkVersionIfOptionProvided(flavor, os, architecture, "dnx46", "40600");
        }

        public void BootstrapperLaunchesRequestedFramworkVersionIfOptionProvided(string flavor, string os, string architecture, string requestedFramework, string expectedOutput)
        {
            const string projectStructure = @"{
    ""project.json"": {},
    ""project.lock.json"": {},
    ""Program.cs"": {}
}";

            const string projectJson = @"{
    ""dependencies"": {
    },
    ""frameworks"": {
        ""dnx46"": {},
        ""dnx452"": {},
        ""dnx451"": {}
    }
}";
            const string lockFile = @"{
  ""locked"": false,
  ""version"": 2,
  ""targets"": {
    ""DNX,Version=v4.5.1"": {},
    ""DNX,Version=v4.5.2"": {},
    ""DNX,Version=v4.6"": {}
  },
  ""libraries"": {},
  ""projectFileDependencyGroups"": {
    """": [],
    ""DNX,Version=v4.5.1"": [],
    ""DNX,Version=v4.5.2"": [],
    ""DNX,Version=v4.6"": []
  }
}";

            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", projectJson)
                    .WithFileContents("project.lock.json", lockFile)
                    .WithFileContents("Program.cs", ClrVersionTestProgram)
                    .WriteTo(tempDir);

                string stdOut;
                string stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: $"--framework {requestedFramework} run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: tempDir);
                Assert.Equal(0, exitCode);
                Assert.Equal(expectedOutput, stdOut.Trim());
            }
        }

        [ConditionalTheory]
        [MemberData(nameof(ClrRuntimeComponents))]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono)]
        [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Linux)]
        public void BootstrapperConfiguresAppConfigFile(string flavor, string os, string architecture)
        {
            const string projectStructure = @"{
    ""project.json"": {},
    ""project.lock.json"": {},
    ""app.config"": {},
    ""Program.cs"": {}
}";

            const string projectJson = @" {
    ""frameworks"": {
        ""dnx451"": {
            ""frameworkAssemblies"": {
                ""System.Configuration"": """"
            }
        }
    }
}";
            const string appConfig = @"<configuration>
  <appSettings>
    <add key=""TheSetting"" value=""TheValue"" />
  </appSettings>
</configuration>";

            var runtimeHomeDir = _fixture.GetRuntimeHomeDir(flavor, os, architecture);
            using (var tempDir = new DisposableDir())
            {
                DirTree.CreateFromJson(projectStructure)
                    .WithFileContents("project.json", projectJson)
                    .WithFileContents("app.config", appConfig)
                    .WithFileContents("Program.cs", AppSettingTestProgram)
                    .WriteTo(tempDir);

                string stdOut;
                string stdErr;
                var exitCode = TestUtils.ExecBootstrapper(
                    runtimeHomeDir,
                    arguments: $"run",
                    stdOut: out stdOut,
                    stdErr: out stdErr,
                    environment: new Dictionary<string, string> { { EnvironmentNames.Trace, null } },
                    workingDir: tempDir);
                Assert.Equal(0, exitCode);
                Assert.Equal("TheSetting=TheValue", stdOut.Trim());
            }
        }
    }
}
