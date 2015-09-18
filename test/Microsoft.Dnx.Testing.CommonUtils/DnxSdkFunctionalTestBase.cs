// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;

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
                if (RuntimeEnvironmentHelper.IsMono)
                {
                    yield return new[] { GetRuntime("mono", string.Empty, string.Empty) };
                }
                else
                {
                    yield return new[] { GetRuntime("clr", "win", "x86") };
                    yield return new[] { GetRuntime("clr", "win", "x64") };
                }
            }
        }

        public static IEnumerable<object[]> CoreClrDnxSdks
        {
            get
            {
                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    yield return new[] { GetRuntime("coreclr", "win", "x86") };
                    yield return new[] { GetRuntime("coreclr", "win", "x64") };
                }
            }
        }

        public static DnxSdk GetRuntime(string flavor, string os, string arch)
        {
            var dnxSolutionRoot = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var runtimeHome = Path.Combine(dnxSolutionRoot, "artifacts", "test", DnxSdk.GetRuntimeName(flavor, os, arch));
            var buildVersion = Environment.GetEnvironmentVariable("DNX_BUILD_VERSION");
            var sdkVersionForTesting = Environment.GetEnvironmentVariable(SdkVersionForTestingEnvName);

            DnxSdk sdk = null;

            if (string.IsNullOrEmpty(sdkVersionForTesting) && 
                Directory.Exists(runtimeHome) && 
                !string.IsNullOrEmpty(buildVersion))
            {
                sdk = DnxSdk.GetRuntime(runtimeHome, $"1.0.0-{buildVersion}", flavor, os, arch);
            }
            else
            {
                sdk = DnxSdk.GetRuntime(SdkVersionForTesting, flavor, os, arch);
            }

            if (!Directory.Exists(sdk.Location))
            {
                throw new InvalidOperationException($"Unable to locate DNX at ${sdk.Location}");
            }

            return sdk;
        }
    }
}
