// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Testing
{
    public class Exec
    {
        public static ExecResult RunScript(string scriptPath,
                                           Action<Dictionary<string, string>> envSetup = null,
                                           string workingDir = null)
        {
            string program;
            string commandLine = "";
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                program = "cmd";
                commandLine = $"/c \"\"{scriptPath}\"";
            }
            else
            {
                program = scriptPath;
            }
            return Run(program, commandLine, envSetup, workingDir);
        }

        public static ExecResult Run(
            string program,
            string commandLine,
            Action<Dictionary<string, string>> envSetup = null,
            string workingDir = null)
        {
            TestLogger.TraceInformation($"Running: {program} {commandLine}");
            var env = new Dictionary<string, string>();
            envSetup?.Invoke(env);

            var processStartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                FileName = program,
                Arguments = commandLine,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var pair in env)
            {
#if DNX451
                processStartInfo.EnvironmentVariables[pair.Key] = pair.Value;
#else
                processStartInfo.Environment[pair.Key] = pair.Value;
#endif
            }

            var process = Process.Start(processStartInfo);
            process.EnableRaisingEvents = true;

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                // If it is not EOF, we always write out a line
                // This should preserve blank lines
                if (args.Data != null)
                {
                    stdoutBuilder.AppendLine(RemoveAnsiColorCodes(args.Data));
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    stderrBuilder.AppendLine(RemoveAnsiColorCodes(args.Data));
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
            var result = new ExecResult()
            {
                StandardError = stderrBuilder.ToString(),
                StandardOutput = stdoutBuilder.ToString(),
                ExitCode = process.ExitCode
            };

            if (result.ExitCode != 0)
            {
                TestLogger.TraceInformation($"Command Output:{Environment.NewLine}{result.StandardOutput}");
                TestLogger.TraceError($"Command Error:{Environment.NewLine}{result.StandardError}");
            }

            return result;
        }

        public static string RemoveAnsiColorCodes(string text)
        {
            var escapeIndex = 0;
            while (true)
            {
                escapeIndex = text.IndexOf("\x1b[", escapeIndex);
                if (escapeIndex != -1)
                {
                    int endIndex = escapeIndex + 3;
                    while (endIndex < text.Length && text[endIndex] != 'm')
                    {
                        ++endIndex;
                    }

                    text = text.Remove(escapeIndex, (endIndex + 1) - escapeIndex);
                }
                else
                {
                    break;
                }
            }

            return text;
        }
    }
}
