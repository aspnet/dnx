using System;
using System.Runtime.InteropServices;
using klr.hosting;

namespace klr.net45.managed
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00eb2481-87a8-4cde-8429-070794b42834")]
    public interface IEntryPoint
    {
        [return: MarshalAs(UnmanagedType.I4)]
        int Execute(
            [In, MarshalAs(UnmanagedType.U4)] uint argc,
            [In, MarshalAs(UnmanagedType.SysInt)] IntPtr argv);
    }

    public unsafe sealed class EntryPoint : IEntryPoint
    {
        public int Execute(uint argc, IntPtr argv)
        {
            var pBstrs = (IntPtr*)argv;
            string[] args = new string[argc];
            for (uint i = 0; i < argc; i++)
            {
                IntPtr thisBstr = pBstrs[i];
                if (thisBstr != IntPtr.Zero)
                {
                    args[i] = Marshal.PtrToStringBSTR(thisBstr);
                }
            }

            return Execute(args);
        }

        private static int Execute(string[] args)
        {
            return RuntimeBootstrapper.Execute(args).Result;
        }
    }
}
