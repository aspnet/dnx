// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Dnx.Host;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.Impl;

[SecurityCritical]
sealed class DomainManager
{
    // this structure is used to pass data from native code
    // and needs to be in sync with its native counterpart
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NativeBootstrapperContext
    {
        public char* OperatingSystem;
        public char* OsVersion;
        public char* Architecture;
        public char* RuntimeDirectory;
        public char* ApplicationBase;
        public bool HandleExceptions;
    }

    [SecurityCritical]
    unsafe static int Execute(int argc, char** argv, NativeBootstrapperContext* context)
    {
        Logger.TraceInformation($"[{nameof(DomainManager)}] Using CoreCLR");

        // Pack arguments
        var arguments = new string[argc];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = new string(argv[i]);
        }

        try
        {
            var bootstrapperContext = new BootstrapperContext();

            bootstrapperContext.OperatingSystem = new string(context->OperatingSystem);
            bootstrapperContext.OsVersion = new string(context->OsVersion);
            bootstrapperContext.Architecture = new string(context->Architecture);
            bootstrapperContext.RuntimeDirectory = new string(context->RuntimeDirectory);
            bootstrapperContext.ApplicationBase = new string(context->ApplicationBase);
            bootstrapperContext.TargetFramework = new FrameworkName(FrameworkNames.LongNames.DnxCore, new Version(5, 0));
            bootstrapperContext.HandleExceptions = context->HandleExceptions;

            Logger.TraceInformation($"Operating System: {bootstrapperContext.OperatingSystem}");
            Logger.TraceInformation($"Operating System Version: {bootstrapperContext.OsVersion}");
            Logger.TraceInformation($"Architecture: {bootstrapperContext.Architecture}");
            Logger.TraceInformation($"Runtime Directory: {bootstrapperContext.RuntimeDirectory}");
            Logger.TraceInformation($"Application Base: {bootstrapperContext.ApplicationBase}");
            Logger.TraceInformation($"Target Framework: {bootstrapperContext.TargetFramework}");
            Logger.TraceInformation($"Handle Exceptions: {bootstrapperContext.HandleExceptions}");

            return RuntimeBootstrapper.Execute(
                arguments,
                bootstrapperContext.TargetFramework,
                bootstrapperContext);
        }
        catch (Exception ex)
        {
            return ex.HResult != 0 ? ex.HResult : 1;
        }
    }
}