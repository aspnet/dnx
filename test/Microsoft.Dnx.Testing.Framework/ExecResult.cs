// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;

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
                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine($"Exit code was {ExitCode}");
                messageBuilder.AppendLine("Stdout:");
                messageBuilder.AppendLine(StandardOutput);
                messageBuilder.AppendLine("Stderr:");
                messageBuilder.AppendLine(StandardError);
                throw new InvalidOperationException(messageBuilder.ToString());
            }

            return this;
        }
    }
}
