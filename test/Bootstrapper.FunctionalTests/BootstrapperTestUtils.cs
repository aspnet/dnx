// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.CommonTestUtils;
using Microsoft.Dnx.Tooling;
using Microsoft.Dnx.Runtime;
using Newtonsoft.Json.Linq;

namespace Bootstrapper.FunctionalTests
{
    public static class BootstrapperTestUtils
    {
        public static int ExecBootstrapper(
            string runtimeHomePath,
            string arguments,
            out string stdOut,
            out string stdErr,
            IDictionary<string, string> environment = null,
            string workingDir = null)
        {
            string runtimeRoot;
            if (string.Equals(Environment.GetEnvironmentVariable("DNX_DEV"), "1"))
            {
                // If DNX_DEV is set, then the path provided is to the root of a package.
                runtimeRoot = runtimeHomePath;
            }
            else
            {
                runtimeRoot = Directory.EnumerateDirectories(Path.Combine(runtimeHomePath, "runtimes"), Constants.RuntimeNamePrefix + "*").First();
            }
            var program = Path.Combine(runtimeRoot, "bin", Constants.BootstrapperExeName);

            string stdOutStr, stdErrStr;
            var exitCode = TestUtils.Exec(program, arguments, out stdOutStr, out stdErrStr, environment, workingDir);
            stdOut = stdOutStr;
            stdErr = stdErrStr;
            return exitCode;
        }
    }
}
