// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dnx.Testing
{
    public class DnxSdkFunctionalTestBase
    {
        public const string SdkVersionForTestingEnvName = "DNX_SDK_VERSION_FOR_TESTING";

        public static string SdkVersionForTesting
        {
            get
            {
                var sdkVersionForTesting = Environment.GetEnvironmentVariable(SdkVersionForTestingEnvName);
                // Warning when DNX_SDK_VERSION_FOR_TESTING is not set?
                return string.IsNullOrEmpty(sdkVersionForTesting) ? "1.0.0-dev" : sdkVersionForTesting;
            }
        }

        public static IEnumerable<object[]> DnxSdks
        {
            get
            {
                return ClrDnxSdks.Concat(CoreClrDnxSdks);
            }
        }

        public static IEnumerable<object[]> ClrDnxSdks
        {
            get
            {
                yield return new[] { DnxSdk.GetRuntime(SdkVersionForTesting, "clr", "win", "x86") };
                yield return new[] { DnxSdk.GetRuntime(SdkVersionForTesting, "clr", "win", "x64") };
            }
        }

        public static IEnumerable<object[]> CoreClrDnxSdks
        {
            get
            {
                yield return new[] { DnxSdk.GetRuntime(SdkVersionForTesting, "coreclr", "win", "x86") };
                yield return new[] { DnxSdk.GetRuntime(SdkVersionForTesting, "coreclr", "win", "x64") };
            }
        }
    }
}
