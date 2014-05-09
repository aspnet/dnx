// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if K10
using System;
using System.Reflection;

namespace System.Diagnostics
{
    internal static class Trace
    {
        public static void TraceError(string message, params object[] args)
        {
            Console.WriteLine("Error: " + message, args);
        }

        public static void TraceInformation(string message, params object[] args)
        {
            Console.WriteLine("Information: " + message, args);
        }

        public static void TraceWarning(string message, params object[] args)
        {
            Console.WriteLine("Warning: " + message, args);
        }
    }
}
#endif