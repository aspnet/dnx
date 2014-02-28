using System;
using System.Runtime.InteropServices;
using System.Security;
using klr.hosting;

[SecurityCritical]
sealed class DomainManager
{
    [SecurityCritical]
    unsafe static int Execute(int argc, char** argv)
    {
        if (argc == 0)
        {
            return 1;
        }

        // Pack arguments
        var arguments = new string[argc];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = new string(argv[i]);
        }

        // TODO: Return a wait handle
        return RuntimeBootstrapper.Execute(arguments).Result;
    }

    /// <summary>
    /// The reverse p/invoke entry point into the KLR.
    /// </summary>
    /// <param name="argc">The number of elements in 'argv'.</param>
    /// <param name="argv">An array of char* elements that represent the command-line parameters.</param>
    /// <param name="ppAsyncAwaiter">When the method returns, will contain an IAsyncAwaiter* which can be used to wait for the exit code.</param>
    /// <returns>An HRESULT. If this returns S_OK, the 'ppAsyncAwaiter' parameter should be populated with an IAsyncAwaiter*.</returns>
    private static unsafe int ExecuteAsync(int argc, char** argv, out IntPtr ppAsyncAwaiter)
    {
        try
        {
            IAsyncAwaiter awaiter = ExecuteAsyncImpl(argc, argv);
            ppAsyncAwaiter = Marshal.GetIUnknownForObject(awaiter);
            return 0; // S_OK
        }
        catch (Exception ex)
        {
            // We cannot allow an unhandled exception to cross the reverse p/invoke boundary.
            // Instead, we call GetHRForException, which has the side effect of calling SetErrorInfo.
            // Our immediate caller can then query GetErrorInfo for the exception object.

            ppAsyncAwaiter = IntPtr.Zero;
            return Marshal.GetHRForException(ex);
        }
    }

    // this method can throw since native code won't directly invoke it
    private static unsafe IAsyncAwaiter ExecuteAsyncImpl(int argc, char** argv)
    {
        if (argc == 0)
        {
            throw new ArgumentOutOfRangeException("argc", "Value must be positive.");
        }
        if (argv == null)
        {
            throw new ArgumentNullException("argv");
        }

        // convert char** -> string[]
        string[] managedArgs = new string[argc];
        for (int i = 0; i < managedArgs.Length; i++)
        {
            char* thisArg = argv[i];
            managedArgs[i] = (thisArg != null) ? new string(thisArg) : null;
        }

        return RuntimeBootstrapper.Execute(managedArgs).GetAwaiter().ToComAwaiter();
    }
}
