// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Runtime
{
    // Rudimentary Logging facility for early in the runtime lifecycle (before DI and Logging come online)
    // Trace Levels (higher levels also display lower level messages):
    //  0 - Off
    //  1 - Information
    //  2 - Verbose
    internal static class Logger
    {
        private const int OffLevel = 0;
        private const int InformationLevel = 1;
        private const int VerboseLevel = 2;

        public static void TraceError(string message, params object[] args)
        {
            if (IsErrorEnabled)
            {
                Console.WriteLine("Error: " + message, args);
            }
        }

        public static void TraceInformation(string message, params object[] args)
        {
            if (IsInformationEnabled)
            {
                Console.WriteLine("Information: " + message, args);
            }
        }

        public static void TraceWarning(string message, params object[] args)
        {
            if (IsWarningEnabled)
            {
                Console.WriteLine("Warning: " + message, args);
            }
        }

        public static void TraceVerbose(string message, params object[] args)
        {
            if (IsVerboseEnabled)
            {
                Console.WriteLine("Verbose: " + message, args);
            }
        }

        public static bool IsErrorEnabled {  get { return TraceLevel >= InformationLevel; } }
        public static bool IsWarningEnabled { get { return TraceLevel >= InformationLevel; } }
        public static bool IsInformationEnabled { get { return TraceLevel >= InformationLevel; } }
        public static bool IsVerboseEnabled { get { return TraceLevel >= VerboseLevel; } }

        private static int? _traceLevel = null;
        public static int TraceLevel
        {
            get
            {
                if (_traceLevel == null)
                {
                    string envVar = Environment.GetEnvironmentVariable(EnvironmentNames.Trace);
                    int newLevel;
                    if (string.IsNullOrWhiteSpace(envVar) || !Int32.TryParse(envVar, out newLevel))
                    {
                        newLevel = 0;
                    }
                    _traceLevel = newLevel;
                }
                return _traceLevel.Value;
            }
        }
    }
}