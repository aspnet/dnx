// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Testing.Framework
{
    internal static class TestLogger
    {
        public static void TraceError(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Test Error: " + message, args);
            }
        }

        public static void TraceInformation(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Test Information: " + message, args);
            }
        }

        public static void TraceWarning(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Test Warning: " + message, args);
            }
        }

        public static bool IsEnabled
        {
            get
            {
                return Environment.GetEnvironmentVariable(TestEnvironmentNames.Trace) == "1";
            }
        }
    }
}