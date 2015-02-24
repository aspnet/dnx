// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Runtime
{
    internal static class Logger
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

        public static bool IsEnabled
        {
            get
            {
                return Environment.GetEnvironmentVariable(EnvironmentNames.Trace) == "1";
            }
        }
    }
}