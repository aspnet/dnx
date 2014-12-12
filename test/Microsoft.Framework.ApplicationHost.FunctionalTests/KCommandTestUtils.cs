// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.FunctionalTestUtils;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.ApplicationHost
{
    public static class KCommandTestUtils
    {
        public static int ExecKCommand(
            string kreHomePath,
            string subcommand,
            string arguments,
            out string stdOut,
            out string stdErr,
            IDictionary<string, string> environment = null,
            string workingDir = null)
        {
            var kreRoot = Directory.EnumerateDirectories(Path.Combine(kreHomePath, "packages"), "KRE-*").First();
            string program, commandLine;
            if (PlatformHelper.IsMono)
            {
                program = Path.Combine(kreRoot, "bin", "k");
                commandLine = string.Format("{0} {1}", subcommand, arguments);
            }
            else
            {
                program = "cmd";
                var kCmdPath = Path.Combine(kreRoot, "bin", "k.cmd");
                commandLine = string.Format("/C {0} {1} {2}", kCmdPath, subcommand, arguments);
            }

            string stdOutStr, stdErrStr;
            var exitCode = TestUtils.Exec(program, commandLine, out stdOutStr, out stdErrStr, environment, workingDir);
            stdOut = stdOutStr;
            stdErr = stdErrStr;
            return exitCode;
        }
    }
}
