// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Framework.ApplicationHost.Impl.Syntax;
using Microsoft.Framework.Runtime;
using NuGet.ProjectModel;

namespace Microsoft.Framework.PackageManager
{
    public class ScriptExecutor
    {
        private static readonly string ErrorMessageTemplate = "The '{0}' script failed with status code {1}.";

        public bool Execute(PackageSpec packageSpec, string scriptName, Func<string, string> getVariable)
        {
            IEnumerable<string> scriptCommandLines;
            if (!packageSpec.Scripts.TryGetValue(scriptName, out scriptCommandLines))
            {
                return true;
            }

            foreach (var scriptCommandLine in scriptCommandLines)
            {
                var scriptArguments = CommandGrammar.Process(
                    scriptCommandLine,
                    GetScriptVariable(packageSpec, getVariable));

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
                            new[] { comSpec, "/C", "\"" }
                            .Concat(scriptArguments)
                            .Concat(new[] { "\"" })
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

                var startInfo = new ProcessStartInfo
                {
                    FileName = scriptArguments.FirstOrDefault(),
                    Arguments = String.Join(" ", scriptArguments.Skip(1)),
                    WorkingDirectory = packageSpec.BaseDirectory,
#if DNX451
                    UseShellExecute = false
#endif
                };

                var process = Process.Start(startInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    ErrorMessage = string.Format(ErrorMessageTemplate, scriptName, process.ExitCode);
                    ExitCode = process.ExitCode;
                    return false;
                }
            }

            return true;
        }

        public int ExitCode { get; private set; }

        public string ErrorMessage { get; private set; }

        private Func<string, string> GetScriptVariable(PackageSpec packageSpec, Func<string, string> getVariable)
        {
            var keys = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "project:Directory", () => packageSpec.BaseDirectory },
                { "project:Name", () => packageSpec.Name },
                { "project:Version", () => packageSpec.Version.ToString() },
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
