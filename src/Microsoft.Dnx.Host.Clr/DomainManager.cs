// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Dnx.Host;
using Microsoft.Dnx.Host.Clr;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.Impl;
using Microsoft.Dnx.Runtime.Json;

public class DomainManager : AppDomainManager
{
    private static readonly Version DefaultFrameworkVersion = new Version(4, 5, 1);

    private ApplicationMainInfo _info;
    private HostExecutionContextManager _hostExecutionContextManager;
    private FrameworkName _dnxTfm;

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

        appDomainInfo.ApplicationBase = _info.RuntimeDirectory;
        appDomainInfo.TargetFrameworkName = DetermineAppDomainTargetFramework();
        appDomainInfo.ConfigurationFile = Path.Combine(_info.ApplicationBase, Constants.AppConfigurationFileName);
    }

    private string DetermineAppDomainTargetFramework()
    {
        // Check if the native bootstrapper gave us a framework name
        string frameworkName = Environment.GetEnvironmentVariable(EnvironmentNames.Framework);
        FrameworkName framework;
        Version version = null;
        if (!string.IsNullOrEmpty(frameworkName))
        {
            if (!FrameworkNameUtility.TryParseFrameworkName(frameworkName, out framework))
            {
                Logger.TraceError($"[{nameof(DomainManager)}] Failed to parse framework name: {frameworkName}");
            }
            else if (!framework.Identifier.Equals(FrameworkNames.LongNames.Dnx, StringComparison.OrdinalIgnoreCase))
            {
                Logger.TraceError($"[{nameof(DomainManager)}] Non-DNX framework name: {frameworkName}");
            }
            else
            {
                // It's a DNX framework! So just use that version as the .NET version
                version = framework.Version;
            }
        }

        // If we didn't get a version from parsing the framework name, use the highest one in project.json
        if (version == null)
        {
            // Calculate it from project.json
            version = SelectHighestSupportedDnxVersion(_info.ApplicationBase);
        }

        // Now that we have a version, build the TFMs and the AppDomain quirking mode TFM
        _dnxTfm = new FrameworkName(FrameworkNames.LongNames.Dnx, version);
        Logger.TraceInformation($"[{nameof(DomainManager)}] Using Desktop CLR v{version}");

        return $"{FrameworkNames.LongNames.NetFramework}, Version=v{version}";
    }

    public override HostExecutionContextManager HostExecutionContextManager
    {
        get
        {
            if (_hostExecutionContextManager == null)
            {
                _hostExecutionContextManager = new DnxHostExecutionContextManager();
            }

            return _hostExecutionContextManager;
        }
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

        return RuntimeBootstrapper.Execute(argv, _dnxTfm);
    }

    private Version SelectHighestSupportedDnxVersion(string applicationBase)
    {
        var projectPath = Path.Combine(applicationBase, "project.json");
        if (!File.Exists(projectPath))
        {
            return DefaultFrameworkVersion;
        }

        try
        {
            // Parse the project
            JsonObject json;
            var jsonText = File.ReadAllText(projectPath);
            using (var reader = new StringReader(jsonText))
            {
                json = JsonDeserializer.Deserialize(reader) as JsonObject;
                if (json == null)
                {
                    Logger.TraceError($"[{nameof(DomainManager)}] project.json did not contain a JSON object at the root.");
                    return DefaultFrameworkVersion;
                }
            }

            // Find the highest DNX desktop version, if any and map it to .NET version
            Version version = null;
            var frameworks = json.ValueAsJsonObject("frameworks")?.Keys;
            if (frameworks != null)
            {
                foreach (var key in frameworks)
                {
                    FrameworkName fx;
                    if (Microsoft.Dnx.Host.FrameworkNameUtility.TryParseFrameworkName(key, out fx) &&
                        fx.Identifier.Equals(FrameworkNames.LongNames.Dnx, StringComparison.Ordinal))
                    {
                        if (version == null || version < fx.Version)
                        {
                            version = fx.Version;
                        }
                    }
                }
            }

            // Return what we found, or just the default framework if we didn't find anything.
            return version ?? DefaultFrameworkVersion;
        }
        catch (Exception ex)
        {
            // If we fail to read the project file, just log and continue
            // We'll have more detailed failures later in the process
            Logger.TraceError($"[{nameof(DomainManager)}] Error reading project.json {ex}");
            return DefaultFrameworkVersion;
        }
    }

    [DllImport(Constants.BootstrapperClrName + ".dll")]
    private extern static void BindApplicationMain(ref ApplicationMainInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct ApplicationMainInfo
    {
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public MainDelegate Main;

        [MarshalAs(UnmanagedType.BStr)]
        public string RuntimeDirectory;

        [MarshalAs(UnmanagedType.BStr)]
        public string ApplicationBase;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int MainDelegate(
        int argc,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 0)] String[] argv);
}
