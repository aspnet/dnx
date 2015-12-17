// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class NativeLibPathUitlsFacts
    {
        [Theory]
        [InlineData("Windows", "10.0", Platform.Windows, "x64", new[] { "win10-x64", "win81-x64", "win8-x64", "win7-x64" })]
        [InlineData("Windows", "10.0", Platform.Windows, "x86", new[] { "win10-x86", "win81-x86", "win8-x86", "win7-x86" })]
        [InlineData("Windows", "6.3", Platform.Windows, "x86", new[] { "win81-x86", "win8-x86", "win7-x86" })]
        [InlineData("Ubuntu", "14.04", Platform.Linux, "x64", new[] { "ubuntu.14.04-x64", "ubuntu-x64" })]
        [InlineData("Darwin", "10.10", Platform.Darwin, "x64", new[] { "osx.10.10-x64", "osx-x64" })]
        public void GetNativeSubfolderCandidatesRetunsExpectedFolders(string os, string version, Platform platform,
            string architecture, string[] expected)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DNX_RUNTIME_ID")))
            {
                return;
            }

            var runtimeEnvironment = new FakeRuntimeEnvironment
            {
                OperatingSystem = os,
                OperatingSystemVersion = version,
                OperatingSystemPlatform = platform,
                RuntimeArchitecture = architecture
            };

            var actual = NativeLibPathUtils.GetNativeSubfolderCandidates(runtimeEnvironment);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetProjectNativeLibPathReturnsCorrectPath()
        {
            var expected = Path.Combine("project", "runtimes", "win7-x86", "native");
            Assert.Equal(expected, NativeLibPathUtils.GetProjectNativeLibPath("project", "win7-x86"));
        }

        [Theory]
        [InlineData("Windows", Platform.Windows, "lib", "lib.dll")]
        [InlineData("Windows", Platform.Windows, "lib", "lib.DLL")]
        [InlineData("Windows", Platform.Windows, "lib.dll", "lib.dll")]
        [InlineData("Windows", Platform.Windows, "lib.DLL", "lib.dll")]
        [InlineData("Linux", Platform.Linux, "lib.so", "lib.so")]
        [InlineData("Linux", Platform.Linux, "lib", "lib.so")]
        [InlineData("Darwin", Platform.Darwin, "lib", "lib.dylib")]
        [InlineData("Darwin", Platform.Darwin, "lib.dylib", "lib.dylib")]
        public void IsMatchingNativeLibraryPositiveMatches(string os, Platform platform, string requestedFileName, string actualFileName)
        {
            Assert.True(NativeLibPathUtils.IsMatchingNativeLibrary(
                new FakeRuntimeEnvironment { OperatingSystem = os, OperatingSystemPlatform = platform }, requestedFileName, actualFileName));
        }

        [Theory]
        [InlineData("Windows", Platform.Windows, "lib.dll.dll", "lib.dll")]
        [InlineData("Linux", Platform.Linux, "LIB.so", "lib.so")]
        [InlineData("Linux", Platform.Linux, "lib", "lib.dylib")]
        [InlineData("Linux", Platform.Linux, "lib", "lib.dll")]
        [InlineData("Darwin", Platform.Darwin, "Lib.dylib", "lib.dylib")]
        [InlineData("Darwin", Platform.Darwin, "lib", "lib.so")]
        [InlineData("Darwin", Platform.Darwin, "lib", "lib.dll")]
        public void IsMatchingNativeLibraryNegativeMatches(string os, Platform platform, string requestedFileName, string actualFileName)
        {
            Assert.False(NativeLibPathUtils.IsMatchingNativeLibrary(
                new FakeRuntimeEnvironment { OperatingSystem = os, OperatingSystemPlatform = platform }, requestedFileName, actualFileName));
        }

        private class FakeRuntimeEnvironment : IRuntimeEnvironment
        {
            public string OperatingSystem { get; set; }

            public Platform OperatingSystemPlatform { get; set; }

            public string OperatingSystemVersion { get; set; }

            public string RuntimeArchitecture { get; set; }

            public string RuntimePath { get; set; }

            public string RuntimeType { get; set; }

            public string RuntimeVersion { get; set; }
        }
    }
}
