// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Framework.ApplicationHost.Impl.Syntax;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    public class ScriptExecutor
    {
        public void Execute(
            Runtime.Project project,
            Reports reports,
            string scriptName,
            Func<string, string> getVariable)
        {
            IEnumerable<string> scriptCommandLines;
            if (!project.Scripts.TryGetValue(scriptName, out scriptCommandLines))
            {
                return;
            }

            foreach (var scriptCommandLine in scriptCommandLines)
            {
                var scriptArguments = CommandGrammar.Process(
                    scriptCommandLine,
                    GetScriptVariable(project, getVariable));

                // Ensure the array won't be empty and the first element won't be null or empty string.
                scriptArguments = scriptArguments.Where(argument => !string.IsNullOrEmpty(argument)).ToArray();

                if (scriptArguments.Length == 0)
                {
                    continue;
                }

                if (!PlatformHelper.IsMono)
                {
                    // Forward-slash is used in script blocked only. Replace them with back-slash to correctly
                    // locate the script. The directory separator is platform-specific. 
                    scriptArguments[0] = scriptArguments[0].Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                    // Command-lines on Windows are executed via "cmd /C" in order
                    // to support batch files, &&, built-in commands like echo, etc.
                    // ComSpec is Windows-specific, and contains the full path to cmd.exe
                    var comSpec = Environment.GetEnvironmentVariable("ComSpec");
                    if (!string.IsNullOrEmpty(comSpec))
                    {
                        scriptArguments =
                            new[] { comSpec, "/C" }
                            .Concat(scriptArguments)
                            .ToArray();
                    }
                }
                else
                {
                    var scriptCandiate = scriptArguments[0] + ".sh";
                    if (File.Exists(scriptCandiate))
                    {
                        scriptArguments[0] = scriptCandiate;
                        scriptArguments = new[] { "/bin/bash" }.Concat(scriptArguments).ToArray();
                    }
                }

                reports.Verbose.WriteLine("Executing '{0}' command:", scriptName.Yellow());
                reports.Verbose.WriteLine("    {0}", string.Join(" ", scriptArguments));
                var startInfo = new ProcessStartInfo
                {
                    FileName = scriptArguments.FirstOrDefault(),
                    Arguments = String.Join(" ", scriptArguments.Skip(1)),
                    WorkingDirectory = project.ProjectDirectory,
#if ASPNET50
                    UseShellExecute = false
#endif
                };

                var process = Process.Start(startInfo);
                process.WaitForExit();
            }
        }

        private Func<string, string> GetScriptVariable(Runtime.Project project, Func<string, string> getVariable)
        {
            var keys = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "project:Directory", () => project.ProjectDirectory },
                { "project:Name", () => project.Name },
                { "project:Version", () => project.Version.ToString() },
            };

            return key =>
            {
                // try returning key from dictionary
                Func<string> valueFactory;
                if (keys.TryGetValue(key, out valueFactory))
                {
                    return valueFactory();
                }

                // try returning command-specific key
                var value = getVariable(key);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                // try returning environment variable
                return Environment.GetEnvironmentVariable(key);
            };
        }
    }
}
