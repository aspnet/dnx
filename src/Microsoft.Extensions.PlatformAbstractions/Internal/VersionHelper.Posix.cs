using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.Extensions.PlatformAbstractions.Internal
{
    internal static partial class VersionHelper
    {
        public static string GetUname()
        {
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
