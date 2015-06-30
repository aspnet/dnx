// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;
using System.Security;
using dnx.host;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.Impl;

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

        return RuntimeBootstrapper.Execute(
            arguments,
            new FrameworkName(FrameworkNames.LongNames.DnxCore, new Version(5, 0)));
    }
}