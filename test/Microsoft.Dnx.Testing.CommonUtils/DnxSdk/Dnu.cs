// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Dnx.Testing
{
    public class Dnu
    {
        private readonly string _sdkPath;

        public Dnu(string sdkPath)
        {
            _sdkPath = sdkPath;
        }

        public ExecResult Publish(
            string projectPath,
            string outputPath,
            string additionalArguments = null,
            Action<Dictionary<string, string>> envSetup = null)
        {
            var sb = new StringBuilder();
            sb.Append($@"publish ""{projectPath}""");
            sb.Append($@" --out ""{outputPath}""");
            sb.Append($" {additionalArguments}");

            return Execute(sb.ToString(), envSetup);
        }

        public ExecResult Restore(
            string restoreDir,
            string packagesDir = null,
            IEnumerable<string> feeds = null,
            string additionalArguments = null)
        {
            var sb = new StringBuilder();
            sb.Append($"restore \"{restoreDir}\"");

            if (!string.IsNullOrEmpty(packagesDir))
            {
                sb.Append($" --packages \"{packagesDir}\"");
            }

            if (feeds != null && feeds.Any())
            {
                sb.Append($" -s {string.Join(" -s ", feeds)}");
            }

            sb.Append($" {additionalArguments}");

            return Execute(sb.ToString());
        }

        public ExecResult PackagesAdd(
            string packagePath,
            string packagesDir,
            string additionalArguments = null,
            Action<Dictionary<string, string>> envSetup = null)
        {
            return Execute($"packages add {packagePath} {packagesDir} {additionalArguments}", envSetup);
        }

        public ExecResult Wrap(
            string csprojPath,
            string additionalArguments = null)
        {
            return Execute($"wrap {csprojPath} {additionalArguments}");
        }

        public DnuPackOutput Pack(
            string projectDir, 
            string outputPath, 
            string configuration = "Debug",
            string additionalArguments = null)
        {
            var sb = new StringBuilder();
            sb.Append($@"pack ""{projectDir}""");
            sb.Append($@" --out ""{outputPath}""");
            sb.Append($" --configuration {configuration}");
            sb.Append($" {additionalArguments}");

            var result = Execute(sb.ToString());

            var projectName = new DirectoryInfo(projectDir).Name;
            return new DnuPackOutput(outputPath, projectName, configuration)
            {
                ExitCode = result.ExitCode,
                StandardError = result.StandardError,
                StandardOutput = result.StandardOutput
            };
        }

        public ExecResult Execute(
            string commandLine, 
            Action<Dictionary<string, string>> envSetup = null)
        {
            var dnxPath = Path.Combine(_sdkPath, "bin", "dnu.cmd");
            return Exec.Run(dnxPath, commandLine, envSetup);
        }
    }
}
