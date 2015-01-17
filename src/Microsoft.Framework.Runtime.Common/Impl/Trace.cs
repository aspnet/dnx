// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if ASPNETCORE50
using System;
using System.Reflection;

namespace System.Diagnostics
{
    internal static class Trace
    {
        public static void TraceError(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Error: " + message, args);
            }
        }

        public static void TraceInformation(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Information: " + message, args);
            }
        }

        public static void TraceWarning(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Warning: " + message, args);
            }
        }

        private static bool IsEnabled
        {
            get
            {
                // TODO: remove KRE_ env var
                return (Environment.GetEnvironmentVariable("DOTNET_TRACE") ?? Environment.GetEnvironmentVariable("KRE_TRACE")) == "1";
            }
        }
    }
}
#endif