// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.Dnx.Tooling
{
    internal class ReportsFactory
    {
        private readonly IRuntimeEnvironment _runtimeEnv;
        private readonly bool _defaultVerbose;

        public ReportsFactory(IRuntimeEnvironment runtimeEnv, bool verbose)
        {
            _runtimeEnv = runtimeEnv;
            _defaultVerbose = verbose;
        }

        public Reports CreateReports(bool quiet)
        {
            return CreateReports(_defaultVerbose, quiet);
        }

        public Reports CreateReports(bool verbose, bool quiet)
        {
            var useConsoleColor = _runtimeEnv.OperatingSystem == "Windows";

            IReport output = new Report(AnsiConsole.GetOutput(useConsoleColor));
            var reports = new Reports()
            {
                Information = output,
                Verbose = verbose ? output : new NullReport(),
                Error = new Report(AnsiConsole.GetError(useConsoleColor)),
            };

            // If "--verbose" and "--quiet" are specified together, "--verbose" wins
            reports.Quiet = quiet ? reports.Verbose : output;

            return reports;
        }
    }
}
