// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Runtime.Common.CommandLine;

namespace Microsoft.Framework.PackageManager
{
    internal class Report : IReport
    {
        private static readonly object _lock = new object();
        private readonly AnsiConsole _console;

        public Report(AnsiConsole console)
        {
            _console = console;
        }

        public void WriteLine(string message)
        {
            lock (_lock)
            {
                _console.WriteLine(message);
            }
        }
    }
}