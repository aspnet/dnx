// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Host;
using Microsoft.Dnx.Runtime;
using Xunit;

namespace dnx.hostTests
{
    public class RuntimeEnvironmentTests
    {
        [Fact]
        public void RuntimeEnvironment_InitializedCorrectly()
        {
            RuntimeEnvironment runtimeEnv =
                new RuntimeEnvironment(
                    new BootstrapperContext
                    {
                        OperatingSystem = "Windows",
                        OsVersion = "10.0",
                        Architecture = "x64",
                        RuntimeType = "CoreClr",
                        RuntimeDirectory = "c:/temp"
                    });

            Assert.Equal(runtimeEnv.OperatingSystem, "Windows");
            Assert.Equal(runtimeEnv.OperatingSystemVersion, "10.0");
            Assert.Equal(runtimeEnv.RuntimeArchitecture, "x64");
            Assert.Equal(runtimeEnv.RuntimeType, "CoreClr");
            Assert.Equal(runtimeEnv.RuntimePath, "c:/temp");
        }

        // Test RID generation
        [Theory]
        [InlineData("Windows", "6.1", "x86", "win7-x86")]
        [InlineData("Windows", "6.1", "x64", "win7-x64")]
        [InlineData("Windows", "6.2", "x86", "win8-x86")]
        [InlineData("Windows", "6.2", "x64", "win8-x64")]
        [InlineData("Windows", "6.3", "x86", "win81-x86")]
        [InlineData("Windows", "6.3", "x64", "win81-x64")]
        [InlineData("Windows", "10.0", "x86", "win10-x86")]
        [InlineData("Windows", "10.0", "x64", "win10-x64")]
        [InlineData("Windows", "10.0", "arm", "win10-arm")]
        [InlineData("Linux", null, "x86", "ubuntu.14.04-x86")]
        [InlineData("Linux", null, "x64", "ubuntu.14.04-x64")]
        [InlineData("Linux", null, "arm", "ubuntu.14.04-arm")]
        [InlineData("Linux", "", "x86", "ubuntu.14.04-x86")]
        [InlineData("Linux", "", "x64", "ubuntu.14.04-x64")]
        [InlineData("Linux", "", "arm", "ubuntu.14.04-arm")]
        [InlineData("Linux", "Ubuntu 14.04", "x86", "ubuntu.14.04-x86")]
        [InlineData("Linux", "Ubuntu 14.04", "x64", "ubuntu.14.04-x64")]
        [InlineData("Linux", "Ubuntu 14.04", "arm", "ubuntu.14.04-arm")]
        [InlineData("Linux", "LinuxMint 17.2", "x86", "ubuntu.14.04-x86")]
        [InlineData("Linux", "LinuxMint 17.2", "x64", "ubuntu.14.04-x64")]
        [InlineData("Linux", "LinuxMint 17.2", "arm", "ubuntu.14.04-arm")]
        [InlineData("Darwin", null, "x86", "osx-x86")]
        [InlineData("Darwin", null, "x64", "osx-x64")]
        [InlineData("Darwin", null, "arm", "osx-arm")]
        [InlineData("Darwin", "", "x86", "osx-x86")]
        [InlineData("Darwin", "", "x64", "osx-x64")]
        [InlineData("Darwin", "", "arm", "osx-arm")]
        [InlineData("Darwin", "10.10", "x86", "osx.10.10-x86")]
        [InlineData("Darwin", "10.10", "x64", "osx.10.10-x64")]
        [InlineData("Darwin", "10.10", "arm", "osx.10.10-arm")]
        public void RuntimeIdIsGeneratedCorrectly(string osName, string version, string architecture, string expectedRid)
        {
            var runtimeEnv = new DummyRuntimeEnvironment()
            {
                OperatingSystem = osName,
                OperatingSystemVersion = version,
                RuntimeArchitecture = architecture
            };
            Assert.Equal(expectedRid, runtimeEnv.GetRuntimeIdentifier());
        }

        [Theory]
        [InlineData("Windows", "6.1", "x86", "win7-x86")]
        [InlineData("Windows", "6.1", "x64", "win7-x64")]
        [InlineData("Windows", "6.2", "x86", "win8-x86,win7-x86")]
        [InlineData("Windows", "6.2", "x64", "win8-x64,win7-x64")]
        [InlineData("Windows", "6.3", "x86", "win81-x86,win8-x86,win7-x86")]
        [InlineData("Windows", "6.3", "x64", "win81-x64,win8-x64,win7-x64")]
        [InlineData("Windows", "10.0", "x86", "win10-x86,win81-x86,win8-x86,win7-x86")]
        [InlineData("Windows", "10.0", "x64", "win10-x64,win81-x64,win8-x64,win7-x64")]
        [InlineData("Linux", "", "x86", "ubuntu.14.04-x86")]
        [InlineData("Linux", "", "x64", "ubuntu.14.04-x64")]
        [InlineData("Linux", "", "arm", "ubuntu.14.04-arm")]
        [InlineData("Linux", "Ubuntu 14.04", "x86", "ubuntu.14.04-x86")]
        [InlineData("Linux", "Ubuntu 14.04", "x64", "ubuntu.14.04-x64")]
        [InlineData("Linux", "Ubuntu 14.04", "arm", "ubuntu.14.04-arm")]
        [InlineData("Linux", "LinuxMint 17.2", "x86", "ubuntu.14.04-x86")]
        [InlineData("Linux", "LinuxMint 17.2", "x64", "ubuntu.14.04-x64")]
        [InlineData("Linux", "LinuxMint 17.2", "arm", "ubuntu.14.04-arm")]
        [InlineData("Darwin", "", "x86", "osx-x86")]
        [InlineData("Darwin", "", "x64", "osx-x64")]
        [InlineData("Darwin", "", "arm", "osx-arm")]

        // Our Darwin RIDs are in flux a bit, but this is just testing that whatever we decide on, we can render the right RID from the right input data :)
        // See: https://github.com/aspnet/dnx/issues/2792
        [InlineData("Darwin", "10.10", "x86", "osx.10.10-x86")]
        [InlineData("Darwin", "10.10", "x64", "osx.10.10-x64")]
        [InlineData("Darwin", "10.10", "arm", "osx.10.10-arm")]
        [InlineData("OSX", "10.10", "x86", "osx.10.10-x86")]
        [InlineData("OSX", "10.10", "x64", "osx.10.10-x64")]
        [InlineData("OSX", "10.10", "arm", "osx.10.10-arm")]
        public void AllRuntimeIdsAreGeneratedCorrectly(string osName, string version, string architecture, string expectedRids)
        {
            var runtimeEnv = new DummyRuntimeEnvironment()
            {
                OperatingSystem = osName,
                OperatingSystemVersion = version,
                RuntimeArchitecture = architecture
            };
            Assert.Equal(expectedRids.Split(','), runtimeEnv.GetAllRuntimeIdentifiers().ToArray());
        }

        // Test live runtime ids
        [ConditionalFact]
        [OSSkipCondition(OperatingSystems.Linux)]
        [OSSkipCondition(OperatingSystems.MacOSX)]
        public void WindowsRuntimeIdIsCorrect()
        {
            // Verifying OS version is difficult in a test

            var expectedArch = RuntimeEnvironmentHelper.RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();
            var rid = RuntimeEnvironmentHelper.RuntimeEnvironment.GetRuntimeIdentifier();
            var osName = new string(rid.TakeWhile(c => !char.IsDigit(c)).ToArray());
            var arch = rid.Split('-')[1];
            Assert.Equal("win", osName);
            Assert.Equal(expectedArch, arch);
        }

        [ConditionalFact]
        [OSSkipCondition(OperatingSystems.Linux)]
        [FrameworkSkipCondition(RuntimeFrameworks.CLR | RuntimeFrameworks.CoreCLR)] // We don't have an OS skip condition for all Windows yet
        public void MacRuntimeIdIsCorrect()
        {
            var expectedArch = RuntimeEnvironmentHelper.RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();
            var rid = RuntimeEnvironmentHelper.RuntimeEnvironment.GetRuntimeIdentifier();
            var osName = new string(rid.TakeWhile(c => c != '.').ToArray());
            var arch = rid.Split('-')[1];
            Assert.Equal("osx", osName);
            Assert.Equal(expectedArch, arch);
        }

        [ConditionalFact]
        [OSSkipCondition(OperatingSystems.MacOSX)]
        [FrameworkSkipCondition(RuntimeFrameworks.CLR | RuntimeFrameworks.CoreCLR)] // We don't have an OS skip condition for all Windows yet
        public void LinuxRuntimeIdIsCorrect()
        {
            var arch = RuntimeEnvironmentHelper.RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();
            Assert.Equal($"ubuntu.14.04-{arch}", RuntimeEnvironmentHelper.RuntimeEnvironment.GetRuntimeIdentifier());
        }

        private class DummyRuntimeEnvironment : IRuntimeEnvironment
        {
            public string OperatingSystem { get; set; }
            public string OperatingSystemVersion { get; set; }
            public string RuntimeArchitecture { get; set; }
            public string RuntimeType { get; set; }
            public string RuntimeVersion { get; set; }
            public string RuntimePath { get; set; }
        }
    }
}
