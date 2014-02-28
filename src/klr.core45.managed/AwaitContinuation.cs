using System;
using System.Runtime.InteropServices;

/// <summary>
/// Represents a method that will be called when an IAsyncAwaiter completes.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate void AwaitContinuation(IntPtr state);
