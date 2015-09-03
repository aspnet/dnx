// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Testing
{
    public class Dnx
    {
        private readonly string _sdkPath;

        public Dnx(string sdkPath)
        {
            _sdkPath = sdkPath;
        }

        public ExecResult Execute(string commandLine, bool dnxTraceOn = true)
        {
            var dnxPath = Path.Combine(_sdkPath, "bin", "dnx");
            return Exec.Run(
                dnxPath,
                commandLine,
                env => env[EnvironmentNames.Trace] = dnxTraceOn ? "1" : null);
        }
    }
}
