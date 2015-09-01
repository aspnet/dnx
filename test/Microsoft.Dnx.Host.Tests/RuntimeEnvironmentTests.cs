// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNet.Testing.xunit;
using Microsoft.Dnx.Runtime;
using Xunit;

namespace dnx.hostTests
{
    public class RuntimeEnvironmentTests
    {
        [Fact]
        public void RuntimeEnvironment_OS()
        {
            RuntimeEnvironment runtimeEnv = new RuntimeEnvironment();

            var os = NativeMethods.Uname();
            if (os == null)
            {
                os = "Windows";
                Assert.NotNull(runtimeEnv.OperatingSystemVersion);
            }
            else
            {
                Assert.Null(runtimeEnv.OperatingSystemVersion);
            }

            Assert.Equal(os, runtimeEnv.OperatingSystem);
        }

        [Fact]
        public void RuntimeEnvironment_RuntimeVersion()
        {
            RuntimeEnvironment runtimeEnv = new RuntimeEnvironment();
            Assert.NotNull(runtimeEnv.RuntimeVersion);
        }

        [Fact]
        public void RuntimeEnvironment_RuntimeArchitecture()
        {
            RuntimeEnvironment runtimeEnv = new RuntimeEnvironment();
            var runtimeArchitecture = IntPtr.Size == 8 ? "x64" : "x86";
            Assert.Equal(runtimeArchitecture, runtimeEnv.RuntimeArchitecture);
        }

        [Fact]
        public void RuntimeEnvironment_RuntimeType()
        {
            RuntimeEnvironment runtimeEnv = new RuntimeEnvironment();
#if DNXCORE50
            Assert.Equal("CoreCLR", runtimeEnv.RuntimeType);
#else
            var runtime = Type.GetType("Mono.Runtime") == null ? "CLR" : "Mono";
            Assert.Equal(runtime, runtimeEnv.RuntimeType);
#endif
        }

        // Test RID generation
        [Theory]
        [InlineData("Windows", "6.1.1234", "x86", "win7-x86")] // 1234 => We only care about major and minor
        [InlineData("Windows", "6.1.1234", "x64", "win7-x64")]
        [InlineData("Windows", "6.2.1234", "x86", "win8-x86")]
        [InlineData("Windows", "6.2.1234", "x64", "win8-x64")]
        [InlineData("Windows", "6.3.1234", "x86", "win81-x86")]
        [InlineData("Windows", "6.3.1234", "x64", "win81-x64")]
        [InlineData("Windows", "10.0.1234", "x86", "win10-x86")]
        [InlineData("Windows", "10.0.1234", "x64", "win10-x64")]
        [InlineData("Windows", "10.0.1234", "arm", "win10-arm")]
        [InlineData("Linux", "", "x86", "linux-x86")]
        [InlineData("Linux", "", "x64", "linux-x64")]
        [InlineData("Linux", "", "arm", "linux-arm")]
        [InlineData("Darwin", "", "x86", "darwin-x86")]
        [InlineData("Darwin", "", "x64", "darwin-x64")]
        [InlineData("Darwin", "", "arm", "darwin-arm")]
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
        [InlineData("Windows", "6.1.1234", "x86", "win7-x86")] // 1234 => We only care about major and minor
        [InlineData("Windows", "6.1.1234", "x64", "win7-x64")]
        [InlineData("Windows", "6.2.1234", "x86", "win8-x86,win7-x86")]
        [InlineData("Windows", "6.2.1234", "x64", "win8-x64,win7-x64")]
        [InlineData("Windows", "6.3.1234", "x86", "win81-x86,win8-x86,win7-x86")]
        [InlineData("Windows", "6.3.1234", "x64", "win81-x64,win8-x64,win7-x64")]
        [InlineData("Windows", "10.0.1234", "x86", "win10-x86,win81-x86,win8-x86,win7-x86")]
        [InlineData("Windows", "10.0.1234", "x64", "win10-x64,win81-x64,win8-x64,win7-x64")]
        [InlineData("Linux", "", "x86", "linux-x86")]
        [InlineData("Linux", "", "x64", "linux-x64")]
        [InlineData("Darwin", "", "x86", "darwin-x86")]
        [InlineData("Darwin", "", "x64", "darwin-x64")]
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
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX)]
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
            var arch = RuntimeEnvironmentHelper.RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();
            Assert.Equal($"darwin-{arch}", RuntimeEnvironmentHelper.RuntimeEnvironment.GetRuntimeIdentifier());
        }

        [ConditionalFact]
        [OSSkipCondition(OperatingSystems.MacOSX)]
        [FrameworkSkipCondition(RuntimeFrameworks.CLR | RuntimeFrameworks.CoreCLR)] // We don't have an OS skip condition for all Windows yet
        public void LinuxRuntimeIdIsCorrect()
        {
            var arch = RuntimeEnvironmentHelper.RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();
            Assert.Equal($"linux-{arch}", RuntimeEnvironmentHelper.RuntimeEnvironment.GetRuntimeIdentifier());
        }

        private class DummyRuntimeEnvironment : IRuntimeEnvironment
        {
            public string OperatingSystem { get; set; }
            public string OperatingSystemVersion { get; set; }
            public string RuntimeArchitecture { get; set; }
            public string RuntimeType { get; set; }
            public string RuntimeVersion { get; set; }
        }
    }
}
