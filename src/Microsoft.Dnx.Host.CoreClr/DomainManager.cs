// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Dnx.Host;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.Impl;

[SecurityCritical]
sealed class DomainManager
{
    [SecurityCritical]
    unsafe static int Execute(int argc, char** argv)
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
            return RuntimeBootstrapper.Execute(
                arguments,
                new FrameworkName(FrameworkNames.LongNames.DnxCore, new Version(5, 0)));
        }
        catch (Exception ex)
        {
            return ex.HResult != 0 ? ex.HResult : 1;
        }
    }
}