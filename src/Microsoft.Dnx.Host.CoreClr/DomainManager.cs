// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Dnx.Host;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Common.Impl;
using Microsoft.Extensions.JsonParser.Sources;

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
            bootstrapperContext.TargetFramework = SelectTargetFramework(bootstrapperContext.ApplicationBase);
            bootstrapperContext.RuntimeType = "CoreClr";

            return RuntimeBootstrapper.Execute(arguments, bootstrapperContext);
        }
        catch (Exception ex)
        {
            return ex.HResult != 0 ? ex.HResult : 1;
        }
    }

    private static readonly FrameworkName DefaultFramework = new FrameworkName(FrameworkNames.LongNames.DnxCore, new Version(5, 0));

    private static FrameworkName SelectTargetFramework(string applicationBase)
    {
        var projectPath = Path.Combine(applicationBase, "project.json");
        if (!File.Exists(projectPath))
        {
            return DefaultFramework;
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
                    return DefaultFramework;
                }
            }

            var frameworks = json.ValueAsJsonObject("frameworks")?.Keys;
            if (frameworks != null)
            {
                foreach (var key in frameworks)
                {
                    FrameworkName fx;
                    if (Microsoft.Dnx.Host.FrameworkNameUtility.TryParseFrameworkName(key, out fx) &&
                        fx.Identifier.Equals(FrameworkNames.LongNames.DnxCore, StringComparison.Ordinal) ||
                        fx.Identifier.Equals(FrameworkNames.LongNames.NetStandardApp, StringComparison.Ordinal) ||
                        fx.Identifier.Equals(FrameworkNames.LongNames.NetCoreApp, StringComparison.Ordinal))
                    {
                        return fx;
                    }
                }
            }

            // Return what we found, or just the default framework if we didn't find anything.
            return DefaultFramework;
        }
        catch (Exception ex)
        {
            // If we fail to read the project file, just log and continue
            // We'll have more detailed failures later in the process
            Logger.TraceError($"[{nameof(DomainManager)}] Error reading project.json {ex}");
            return DefaultFramework;
        }
    }

}