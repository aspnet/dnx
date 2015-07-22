// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Tooling
{
    public static class ReportExtensions
    {
        public static void WriteLine(this IReport report)
        {
            report.WriteLine(string.Empty);
        }

        public static void WriteLine(this IReport report, string format, params object[] args)
        {
            report.WriteLine(string.Format(format, args));
        }
    }
}