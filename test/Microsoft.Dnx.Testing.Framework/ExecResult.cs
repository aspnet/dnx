// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Testing.Framework
{
    public class ExecResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }

        public ExecResult EnsureSuccess()
        {
            if (ExitCode != 0)
            {
                throw new InvalidOperationException($"Exit code was {ExitCode}");
            }

            return this;
        }
    }
}
