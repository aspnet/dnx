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
    }
}