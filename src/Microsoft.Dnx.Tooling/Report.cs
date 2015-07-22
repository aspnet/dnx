// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
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