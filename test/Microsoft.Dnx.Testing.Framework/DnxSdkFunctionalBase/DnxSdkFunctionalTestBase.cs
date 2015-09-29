// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Testing.Framework
{
    public class DnxSdkFunctionalTestBase
    {
        public static bool UseCustomSdks
        {
            get
            {
                return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestEnvironmentNames.Runtimes));
            }
        }

        public static string SdkVersionForTesting
        {
            get
            {
                var sdkVersionForTesting = Environment.GetEnvironmentVariable(TestEnvironmentNames.TestSdkVersion);
                // Warning when DNX_SDK_VERSION_FOR_TESTING is not set?
                return string.IsNullOrEmpty(sdkVersionForTesting) ? "1.0.0-dev" : sdkVersionForTesting;
            }
        }

        public static IEnumerable<object[]> DnxSdks
        {
            get
            {
                if (UseCustomSdks)
                {
                    return CustomDnxSdks;
                }
                else
                {
                    return ClrDnxSdks.Concat(CoreClrDnxSdks);
                }
            }
        }

        public static IEnumerable<object[]> ClrDnxSdks
        {
            get
            {
                if (UseCustomSdks)
                {
                    foreach (var sdk in CustomDnxSdks)
                    {
                        yield return sdk;
                    }
                }
                else if (RuntimeEnvironmentHelper.IsMono)
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
                if (UseCustomSdks)
                {
                    foreach (var sdk in CustomDnxSdks)
                    {
                        yield return sdk;
                    }
                }
                else if (RuntimeEnvironmentHelper.IsWindows)
                {
                    yield return new[] { GetRuntime("coreclr", "win", "x86") };
                    yield return new[] { GetRuntime("coreclr", "win", "x64") };
                }
                else
                {
                    yield break;
                }
            }
        }

        public static IEnumerable<object[]> CustomDnxSdks
        {
            get
            {
                var sdks = Environment.GetEnvironmentVariable(TestEnvironmentNames.Runtimes).Split(';');
                foreach (var sdk in sdks)
                {
                    if (sdk == "mono")
                    {
                        yield return new[] { GetRuntime("mono", string.Empty, string.Empty) };
                    }

                    var runtimeDefinition = sdk.Split('-');
                    yield return new[] { GetRuntime(runtimeDefinition[0], runtimeDefinition[1], runtimeDefinition[2]) };
                }
            }
        }

        public static DnxSdk GetRuntime(string flavor, string os, string arch)
        {
            var dnxSolutionRoot = ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var runtimeHome = Path.Combine(dnxSolutionRoot, "artifacts", "test", DnxSdk.GetRuntimeName(flavor, os, arch));
            var buildVersion = Environment.GetEnvironmentVariable("DNX_BUILD_VERSION");
            var sdkVersionForTesting = Environment.GetEnvironmentVariable(TestEnvironmentNames.TestSdkVersion);

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
