// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.Impl;

namespace Microsoft.Dnx.Host.Clr
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00eb2481-87a8-4cde-8429-070794b42834")]
    public interface IEntryPoint
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        IAwaiter Execute(
            [In, MarshalAs(UnmanagedType.U4)] uint argc,
            [In, MarshalAs(UnmanagedType.SysInt)] IntPtr argv);
    }

    public delegate void OnCompletedCallback(IntPtr state);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("39F2F3DC-656C-41D1-9CD8-41307924DFAC")]
    public interface IAwaiter
    {
        bool IsCompleted
        {
            [return: MarshalAs(UnmanagedType.Bool)]
            get;
        }

        [return: MarshalAs(UnmanagedType.I4)]
        int GetResult();

        void UnsafeOnCompleted(
            [In, MarshalAs(UnmanagedType.FunctionPtr)] OnCompletedCallback callback,
            [In, MarshalAs(UnmanagedType.SysInt)] IntPtr state);

    }

    public unsafe sealed class EntryPoint : IEntryPoint
    {
        public IAwaiter Execute(uint argc, IntPtr argv)
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

            // Helios boots the DNX through this entry point. It has already
            // set up the AppDomain though, so we just need to sniff the target framework based
            // on the value provided by during AppDomain setup
            var fxName = AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName;
            var version = new Version(4, 5, 1);
            if (!string.IsNullOrEmpty(fxName))
            {
                version = new FrameworkName(fxName).Version;
            }

            return new Awaiter(RuntimeBootstrapper.ExecuteAsync(
                args,
                new FrameworkName(FrameworkNames.LongNames.Dnx, version)));
        }

        private sealed class Awaiter : IAwaiter
        {
            private readonly Task<int> _task;

            public Awaiter(Task<int> task)
            {
                _task = task;
            }

            public bool IsCompleted
            {
                get { return _task.GetAwaiter().IsCompleted; }
            }

            public int GetResult()
            {
                return _task.GetAwaiter().GetResult();
            }

            public void UnsafeOnCompleted(OnCompletedCallback callback, IntPtr state)
            {
                _task.GetAwaiter().UnsafeOnCompleted(() => callback(state));
            }
        }
    }
}

