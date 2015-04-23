// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.CommonTestUtils;
using Microsoft.Framework.PackageManager;
using Microsoft.Framework.Runtime;
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
            var runtimeRoot = Directory.EnumerateDirectories(Path.Combine(runtimeHomePath, "runtimes"), Constants.RuntimeNamePrefix + "*").First();
            var program = Path.Combine(runtimeRoot, "bin", Constants.BootstrapperExeName);

            string stdOutStr, stdErrStr;
            var exitCode = TestUtils.Exec(program, arguments, out stdOutStr, out stdErrStr, environment, workingDir);
            stdOut = stdOutStr;
            stdErr = stdErrStr;
            return exitCode;
        }

        public static DisposableDir PrepareTemporarySamplesFolder(string runtimeHomeDir)
        {
            var tempDir = new DisposableDir();
            TestUtils.CopyFolder(TestUtils.GetSamplesFolder(), tempDir);

            // Make sure sample projects depend on runtime components from newly built dnx
            var currentDnxSolutionRootDir = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var currentDnxSolutionSrcPath = Path.Combine(currentDnxSolutionRootDir, "src").Replace("\\", "\\\\");
            var samplesGlobalJson = new JObject();
            samplesGlobalJson["projects"] = new JArray(new[] { currentDnxSolutionSrcPath });
            File.WriteAllText(Path.Combine(tempDir, GlobalSettings.GlobalFileName), samplesGlobalJson.ToString());

            // Make sure package restore can be successful
            const string nugetConfigName = "NuGet.Config";
            File.Copy(Path.Combine(currentDnxSolutionRootDir, nugetConfigName), Path.Combine(tempDir, nugetConfigName));

            // Use the newly built runtime to generate lock files for samples
            string stdOut, stdErr;
            int exitCode;
            foreach (var projectDir in Directory.EnumerateDirectories(tempDir))
            {
                exitCode = DnuTestUtils.ExecDnu(
                    runtimeHomeDir,
                    subcommand: "restore",
                    arguments: projectDir,
                    stdOut: out stdOut,
                    stdErr: out stdErr);

                if (exitCode != 0)
                {
                    Console.WriteLine(stdOut);
                    Console.WriteLine(stdErr);
                }
            }

            return tempDir;
        }
    }
}
