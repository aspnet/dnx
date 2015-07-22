// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
    }
}
