using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Extensions.PlatformAbstractions
{
    public class DefaultRuntimeEnvironment : IRuntimeEnvironment
    {
        public DefaultRuntimeEnvironment()
        {
            OperatingSystem = GetOs();
            RuntimePath = GetLocation(typeof(object).GetTypeInfo().Assembly);
            RuntimeType = GetRuntimeType();
            RuntimeVersion = typeof(object).GetTypeInfo().Assembly.GetName().Version.ToString();
            RuntimeArchitecture = GetArch();
        }

        // TODO: implement
        public string OperatingSystemVersion { get; }

        public string OperatingSystem { get; }

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

        private string GetOs()
        {
#if NET451
            var platform = (int)Environment.OSVersion.Platform;
            var isWindows = (platform != 4) && (platform != 6) && (platform != 128);

            if (isWindows)
            {
                return "Windows";
            }
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "Windows";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "Darwin";
            }
#endif
            return GetUname();
        }

        private static string GetArch()
        {
#if NET451
            return Environment.Is64BitProcess ? "x64" : "x86";
#else
            return IntPtr.Size == 8 ? "x64" : "x86";
#endif
        }

        private unsafe string GetUname()
        {

            var buffer = new byte[8192];
            try
            {
                fixed (byte* buf = buffer)
                {
                    if (uname((IntPtr)buf) == 0)
                    {
                        return Marshal.PtrToStringAnsi((IntPtr)buf);
                    }
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        [DllImport("libc")]
        static extern int uname(IntPtr buf);
    }
}
