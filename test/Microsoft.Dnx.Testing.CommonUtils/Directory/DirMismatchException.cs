// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit.Sdk;

namespace Microsoft.Dnx.Testing
{
    public class DirMismatchException : XunitException
    {
        public DirMismatchException(string expectedDirPath, string actualDirPath, DirDiff diff)
        {
            Message = $@"The actual directory structure '{actualDirPath}' doesn't match expected structure '{expectedDirPath}'.
Difference information:
Extra: {string.Join(", ", diff.ExtraEntries)}
Missing: {string.Join(", ", diff.MissingEntries)}
Different: {string.Join(", ", diff.DifferentEntries)}";
        }

        public override string Message { get; }
    }
}
