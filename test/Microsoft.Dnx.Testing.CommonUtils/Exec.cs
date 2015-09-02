using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Dnx.Testing
{
    public class ExecResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }

        public ExecResult EnsureSuccess()
        {
            if (ExitCode != 0)
            {
                throw new InvalidOperationException($"Exit code was {ExitCode}");
            }

            return this;
        }
    }

    public class Exec
    {
        public static ExecResult Run(
            string program,
            string commandLine,
            Action<Dictionary<string, string>> envSetup = null,
            string workingDir = null)
        {
            Console.WriteLine($"Running: {program} {commandLine}");
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
                processStartInfo.Environment.Add(pair);
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
                    Console.WriteLine(args.Data);
                    stdoutBuilder.AppendLine(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Console.WriteLine(args.Data);
                    stderrBuilder.AppendLine(args.Data);
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

            return result;
        }
    }
}
