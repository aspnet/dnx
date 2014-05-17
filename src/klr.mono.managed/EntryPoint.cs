// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using klr.hosting;

public class EntryPoint
{
    public static int Main(string[] arguments)
    {
        // Set the default lib path to be next to the entry point location
        Environment.SetEnvironmentVariable("KRE_DEFAULT_LIB", Path.GetDirectoryName(typeof(EntryPoint).Assembly.Location));
        Environment.SetEnvironmentVariable("KRE_CONSOLE_HOST", "1");
        
        return RuntimeBootstrapper.Execute(arguments);
    }
}