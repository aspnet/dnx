// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace Microsoft.Dnx.Tooling
{
    public static class CommandNameValidator
    {
        private static readonly string[] BlockedCommandNames = new string[]
        {
            "dnvm",
            "dnx",
            "dotnet",
            "dotnetsdk",
            "dnu",
            "nuget"
        };

        private static readonly string[] SkippedCommandNames = new string[]
        {
            "run",
            "test",
            "web"
        };

        public static bool IsCommandNameValid(string commandName)
        {
            // TODO: Make the comparison case sensitive of Linux?
            return
                !string.IsNullOrWhiteSpace(commandName) &&
                !BlockedCommandNames.Contains(commandName, StringComparer.OrdinalIgnoreCase);
        }

        public static bool ShouldNameBeSkipped(string commandName)
        {
            return SkippedCommandNames.Contains(commandName);
        }
    }
}
