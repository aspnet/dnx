// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.FunctionalTestUtils;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    public static class KpmTestUtils
    {
        public static int ExecKpm(string kreHomePath, string subcommand, string arguments,
            IDictionary<string, string> environment = null, string workingDir = null)
        {
            var kreRoot = Directory.EnumerateDirectories(Path.Combine(kreHomePath, "packages"), "KRE-*").First();
            string program, commandLine;
            if (PlatformHelper.IsMono)
            {
                program = Path.Combine(kreRoot, "bin", "kpm");
                commandLine = string.Format("{0} {1}", subcommand, arguments);
            }
            else
            {
                program = "cmd";
                var kpmCmdPath = Path.Combine(kreRoot, "bin", "kpm.cmd");
                commandLine = string.Format("/C {0} {1} {2}", kpmCmdPath, subcommand, arguments);
            }

            string stdOut, stdErr;
            return TestUtils.Exec(program, commandLine, out stdOut, out stdErr, environment, workingDir);
        }
    }
}
