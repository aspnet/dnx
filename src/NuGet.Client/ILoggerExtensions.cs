// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Client
{
    public static class ILoggerExtensions
    {
        public static void WriteVerbose(this ILogger logger, string message, params object[] args)
        {
            logger.WriteVerbose(string.Format(message, args));
        }

        public static void WriteInformation(this ILogger logger, string message, params object[] args)
        {
            logger.WriteInformation(string.Format(message, args));
        }

        public static void WriteError(this ILogger logger, string message, params object[] args)
        {
            logger.WriteError(string.Format(message, args));
        }

        public static void WriteQuiet(this ILogger logger, string message, params object[] args)
        {
            logger.WriteQuiet(string.Format(message, args));
        }
    }
}