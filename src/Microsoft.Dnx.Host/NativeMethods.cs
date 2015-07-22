// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Dnx.Runtime
{
    internal static class NativeMethods
    {
        public unsafe static string Uname()
        {
            var buffer = stackalloc byte[8192];
            try
            {
                if (uname((IntPtr)buffer) == 0)
                {
                    return Marshal.PtrToStringAnsi((IntPtr)buffer);
                }
            }
            catch
            {
            }

            return null;
        }

        // Linux and Mac import
        [DllImport("libc")]
        private static extern int uname(IntPtr buf);

#if DNXCORE50
        public static Version OSVersion
        {
            get
            {
                uint dwVersion = GetVersion();

                int major = (int)(dwVersion & 0xFF);
                int minor = (int)((dwVersion >> 8) & 0xFF);

                return new Version(major, minor);
            }
        }

        private static uint GetVersion()
        {
            try
            {
                return GetVersion_ApiSet();
            }
            catch
            {
                try
                {
                    return GetVersion_Kernel32();
                }
                catch
                {
                    return 0;
                }
            }

        }

        // The API set is required by OneCore based systems
        // and it is available only on Win 8 and newer
        [DllImport("api-ms-win-core-sysinfo-l1-2-1", EntryPoint = "GetVersion")]
        private static extern uint GetVersion_ApiSet();

        // For Win 7 and Win 2008 compatibility
        [DllImport("kernel32.dll", EntryPoint = "GetVersion")]
        private static extern uint GetVersion_Kernel32();
#endif
    }
}
