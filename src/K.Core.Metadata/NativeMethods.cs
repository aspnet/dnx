using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using K.Core.Metadata;

public class NativeMethods
{
    [DllImport("rometadata.dll", PreserveSig = false, CharSet=CharSet.Unicode)]
    internal static extern IMetaDataDispenser MetaDataGetDispenser(ref Guid clsID, ref Guid iid);

    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    internal static extern uint GetEnvironmentVariable(string lpName, [Out] StringBuilder lpBuffer, uint nSize);

    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    internal static extern bool CreateProcess(string lpApplicationName,
        string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
        ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles,
        uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
        [In] ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

}

[StructLayout(LayoutKind.Sequential)]
public struct SECURITY_ATTRIBUTES
{
    public int nLength;
    public IntPtr lpSecurityDescriptor;
    public int bInheritHandle;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct STARTUPINFO
{
    public Int32 cb;
    public string lpReserved;
    public string lpDesktop;
    public string lpTitle;
    public Int32 dwX;
    public Int32 dwY;
    public Int32 dwXSize;
    public Int32 dwYSize;
    public Int32 dwXCountChars;
    public Int32 dwYCountChars;
    public Int32 dwFillAttribute;
    public Int32 dwFlags;
    public Int16 wShowWindow;
    public Int16 cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;
    public IntPtr hStdOutput;
    public IntPtr hStdError;
}

[StructLayout(LayoutKind.Sequential)]
public struct PROCESS_INFORMATION
{
    public IntPtr hProcess;
    public IntPtr hThread;
    public int dwProcessId;
    public int dwThreadId;
}

public class ProcessWaitHandle : WaitHandle
{
    public ProcessWaitHandle(IntPtr processHandle)
    {
        this.SafeWaitHandle = new SafeWaitHandle(processHandle, false);
    }
}
