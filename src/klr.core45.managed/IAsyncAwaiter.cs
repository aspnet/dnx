using System;
using System.Runtime.InteropServices;

/// <summary>
/// Represents a COM wrapper around TaskAwaiter&lt;int&gt;.
/// </summary>
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("2EB91A4C-BE8E-463B-9ABC-1A9CA1C37EC3")]
public interface IAsyncAwaiter
{
    /// <summary>
    /// Returns a value stating whether the operation is completed.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Returns the result of this operation, blocking if necessary.
    /// </summary>
    int GetResult();

    /// <summary>
    /// Enqueues a continuation routine that will be dispatched when the asynchronous operation finishes.
    /// </summary>
    /// <param name="pfnContinuation">The continuation routine to invoke when the operation completes. This function
    /// pointer must translate to an 'AwaitContinuation' routine.</param>
    /// <param name="vState">The state parameter to provide to the continuation routine.</param>
    /// <remarks>The continuation routine is guaranteed not to be invoked inline on the thread which called 'OnCompleted'.</remarks>
    void OnCompleted(IntPtr pfnContinuation, IntPtr vState);
}
