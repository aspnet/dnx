using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.PlatformAbstractions.Internal;

namespace Microsoft.Extensions.PlatformAbstractions
{
    public class DefaultRuntimeEnvironment : IRuntimeEnvironment
    {
        public DefaultRuntimeEnvironment()
        {
            LoadOsInfo();
            RuntimePath = GetLocation(typeof(object).GetTypeInfo().Assembly);
            RuntimeType = GetRuntimeType();
            RuntimeVersion = typeof(object).GetTypeInfo().Assembly.GetName().Version.ToString();
            RuntimeArchitecture = GetArch();
        }

        // TODO: implement
        public string OperatingSystemVersion { get; private set; }

        public string OperatingSystem { get; private set; }

        public string RuntimeArchitecture { get; }

        public string RuntimePath { get; }

        public string RuntimeType { get; }

        public string RuntimeVersion { get; }

        private string GetRuntimeType()
        {
#if NET451
            return Type.GetType("Mono.Runtime") != null ? "Mono" : "CLR";
#else
            return "CoreCLR";
#endif
        }

        private string GetLocation(Assembly assembly)
        {
            string assemblyLocation = null;
#if NET451
            assemblyLocation = assembly.Location;
#else
            assemblyLocation = typeof(Assembly).GetRuntimeProperty("Location").GetValue(assembly, index: null) as string;
#endif
            return string.IsNullOrEmpty(assemblyLocation) ? null : Path.GetDirectoryName(assemblyLocation);
        }

        private void LoadOsInfo()
        {
#if NET451
            var platform = (int)Environment.OSVersion.Platform;
            var isWindows = (platform != 4) && (platform != 6) && (platform != 128);

            if (isWindows)
            {
                OperatingSystem = "Windows";
                OperatingSystemVersion = VersionHelper.GetWindowsVersion();
            }
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                OperatingSystem = "Windows";
                OperatingSystemVersion = VersionHelper.GetWindowsVersion();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                OperatingSystem = VersionHelper.GetLinuxOsName();
                OperatingSystemVersion = VersionHelper.GetLinuxVersion();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                OperatingSystem = "Darwin";
                OperatingSystemVersion = VersionHelper.GetOSXVersion();
            }
#endif
            else
            {
                OperatingSystem = VersionHelper.GetUname();
            }
        }

        private static string GetArch()
        {
#if NET451
            return Environment.Is64BitProcess ? "x64" : "x86";
#else
            return IntPtr.Size == 8 ? "x64" : "x86";
#endif
        }

    }
}
