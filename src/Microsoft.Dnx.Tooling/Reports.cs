// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Tooling
{
    public class Reports
    {
        public IReport Information { get; set; }
        public IReport Verbose { get; set; }
        public IReport Quiet { get; set; }
        public IReport Error { get; set; }

        public static class Constants
        {
            public static readonly IReport NullReport = new NullReport();
            public static readonly Reports NullReports = new Reports
            {
                Information = NullReport,
                Verbose = NullReport,
                Quiet = NullReport,
                Error = NullReport
            };
        }

        public void WriteInformation(string message)
        {
            Information.WriteLine(message);
        }

        public void WriteVerbose(string message)
        {
            Verbose.WriteLine(message);
        }

        public void WriteError(string message)
        {
            Error.WriteLine(message.Red());
        }

        public void WriteWarning(string message)
        {
            Information.WriteLine(message.Yellow());
        }

        public Reports ShallowCopy()
        {
            return MemberwiseClone() as Reports;
        }

        private class NullReport : IReport
        {
            public void WriteLine(string message)
            {
                // Consume the write operation and do nothing
                // Used when verbose option is not specified
            }
        }
    }
}