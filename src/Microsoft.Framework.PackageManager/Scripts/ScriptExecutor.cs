using Microsoft.Framework.ApplicationHost.Impl.Syntax;
using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Framework.PackageManager
{
    /// <summary>
    /// Summary description for ScriptExecutor
    /// </summary>
    public class ScriptExecutor
    {
        public void Execute(Runtime.Project project, string scriptName, Func<string, string> getVariable)
        {
            string scriptCommandLine;
            if (!project.Scripts.TryGetValue(scriptName, out scriptCommandLine))
            {
                return;
            }

            var scriptArguments = CommandGrammar.Process(
                scriptCommandLine,
                GetScriptVariable(project, getVariable));

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

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptArguments.FirstOrDefault(),
                Arguments = String.Join(" ", scriptArguments.Skip(1)),
                WorkingDirectory = project.ProjectDirectory,
#if NET45
                UseShellExecute = false,
#endif
            };
            var process = Process.Start(startInfo);

            process.WaitForExit();
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