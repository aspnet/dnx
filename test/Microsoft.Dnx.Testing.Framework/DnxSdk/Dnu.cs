// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Testing.Framework
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
            Solution solution,
            string packagesDir = null,
            IEnumerable<string> feeds = null,
            string additionalArguments = null)
        {
            return Restore(solution.RootPath, packagesDir, feeds, additionalArguments);
        }
        
        public ExecResult Restore(
            Project project,
            string packagesDir = null,
            IEnumerable<string> feeds = null,
            string additionalArguments = null)
        {
            return Restore(project.ProjectDirectory, packagesDir, feeds, additionalArguments);
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
            return Execute($"packages add \"{packagePath}\" \"{packagesDir}\" {additionalArguments}", envSetup);
        }

        public ExecResult Wrap(
            string csprojPath,
            string additionalArguments = null)
        {
            return Execute($"wrap \"{csprojPath}\" {additionalArguments}");
        }

        public DnuPackOutput Build(
            string projectDir, 
            string outputPath = null, 
            string configuration = "Debug",
            string additionalArguments = null)
        {
            return PackOrBuild("build", projectDir, outputPath, configuration, additionalArguments);
        }

        public DnuPackOutput Pack(
            string projectDir, 
            string outputPath = null, 
            string configuration = "Debug",
            string additionalArguments = null)
        {
            return PackOrBuild("pack", projectDir, outputPath, configuration, additionalArguments);
        }

        private DnuPackOutput PackOrBuild(
            string dnuCommand,
            string projectDir, 
            string outputPath, 
            string configuration,
            string additionalArguments)
        {
            var sb = new StringBuilder();
            sb.Append($@"pack ""{projectDir}""");
            if (!string.IsNullOrEmpty(outputPath))
            {
                sb.Append($@" --out ""{outputPath}""");
            }
            sb.Append($" --configuration {configuration}");
            sb.Append($" {additionalArguments}");

            var result = Execute(sb.ToString());

            var projectName = new DirectoryInfo(projectDir).Name;
            return new DnuPackOutput(outputPath ?? Path.Combine(projectDir, "bin"), projectName, configuration)
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
            string command;
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                command = "cmd";
                commandLine = $"/C \"\"{Path.Combine(_sdkPath, "bin", "dnu.cmd")}\" {commandLine}\"";
            }
            else
            {
                command = Path.Combine(_sdkPath, "bin", "dnu");
            }
            return Exec.Run(command, commandLine, envSetup);
        }
    }
}
