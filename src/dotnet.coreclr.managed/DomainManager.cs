// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Security;
using System.Threading;
using dotnet.hosting;

[SecurityCritical]
sealed class DomainManager
{
    [SecurityCritical]
    unsafe static int Execute(int argc, char** argv)
    {
        // Pack arguments
        var arguments = new string[argc];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = new string(argv[i]);
        }

        return RuntimeBootstrapper.Execute(arguments);
    }
}