using System;
using System.Runtime.InteropServices;

namespace Microsoft.Extensions.PlatformAbstractions.Internal
{
    internal static partial class VersionHelper
    {
        private static Lazy<utsname> _unameData = new Lazy<utsname>(GetUnameData);
        
        internal static utsname UnameData => _unameData.Value;
        
        private static utsname GetUnameData()
        {
            try
            {
                utsname result = new utsname();
                if (uname(ref result) == 0)
                {
                    return result;
                }
            }
            catch
            {
                // Should we do this? If we wanted the uname data but failed to read it, that's really bad...
            }
            return new utsname();
        }

        [DllImport("libc")]
        private static extern int uname(ref utsname result);
        
        internal struct utsname
        {
            public string sysname;
            public string nodename;
            public string release;
            public string version;
            public string machine;
        }
    }
}
