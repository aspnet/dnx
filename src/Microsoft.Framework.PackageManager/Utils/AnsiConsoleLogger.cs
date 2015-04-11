// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Client;

namespace Microsoft.Framework.PackageManager
{
    public class AnsiConsoleLogger : ILogger
    {
        private readonly AnsiConsole _console;
        private readonly bool _verbose;
        private readonly bool _quiet;
        private readonly object _lockObj = new object();

        public AnsiConsoleLogger(bool verbose, bool quiet)
        {
            _console = AnsiConsole.Output;
            _verbose = verbose;
            _quiet = quiet;
        }

        public void WriteVerbose(string message)
        {
            if (_verbose)
            {
                WriteLine(message);
            }
        }

        public void WriteError(string message)
        {
            WriteLine(message);
        }

        public void WriteInformation(string message)
        {
            WriteLine(message);
        }

        public void WriteQuiet(string message)
        {
            if (!_verbose && _quiet)
            {
                WriteLine(message);
            }
        }

        private void WriteLine(string message)
        {
            lock (_lockObj)
            {
                _console.WriteLine(message);
            }
        }
    }
}