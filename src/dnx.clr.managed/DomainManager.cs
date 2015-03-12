// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using dnx.host;
using Microsoft.Framework.Runtime;

public class DomainManager : AppDomainManager
{
    private ApplicationMainInfo _info;

    public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
    {
        // Do nothing if this isn't the default app domain
        if (!AppDomain.CurrentDomain.IsDefaultAppDomain())
        {
            return;
        }

        _info.Main = Main;
        BindApplicationMain(ref _info);

        if (!string.IsNullOrEmpty(_info.ApplicationBase))
        {
            Environment.SetEnvironmentVariable(EnvironmentNames.AppBase, _info.ApplicationBase);
        }

        appDomainInfo.ApplicationBase = Environment.GetEnvironmentVariable(EnvironmentNames.DefaultLib);
        appDomainInfo.TargetFrameworkName = ".NETFramework,Version=v4.5.1";
    }

    private int Main(int argc, string[] argv)
    {
        // Create the socket on a new thread to warm up the configuration stack
        // before any other code starts to run. This allows us to startup up much
        // faster.
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        },
        null);

        return RuntimeBootstrapper.Execute(argv);
    }

    [DllImport(Constants.BootstrapperClrName + ".dll")]
    private extern static void BindApplicationMain(ref ApplicationMainInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct ApplicationMainInfo
    {
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public MainDelegate Main;

        [MarshalAs(UnmanagedType.BStr)]
        public String ApplicationBase;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int MainDelegate(
        int argc,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 0)] String[] argv);
}
