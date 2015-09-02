using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dnx.Testing
{
    public class DnxSdkFunctionalTestBase
    {
        public const string SdkVersionForTestingEnvName = "DNX_SDK_VERSION_FOR_TESTING";

        static DnxSdkFunctionalTestBase()
        {
            Console.WriteLine($@"
Environment information:
  DNX_HOME: {Environment.GetEnvironmentVariable("DNX_HOME")}
  DNX_SDK_VERSION_FOR_TESTING: {Environment.GetEnvironmentVariable("DNX_SDK_VERSION_FOR_TESTING")}

Information of DNX under testing:
  DNX Home: {DnxSdk.GetRuntimeHome()}
  DNX Version: {SdkVersionForTesting}
");
        }

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
