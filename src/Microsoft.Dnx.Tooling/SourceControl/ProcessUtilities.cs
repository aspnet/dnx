// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling.SourceControl
{
    internal static class ProcessUtilities
    {
        private const int DefaultProcessTimeout = 5 * 60 * 1000;

        public static bool ExecutableExists(string executableName)
        {
            string whereApp;
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                whereApp = "where";
            }
            else
            {
                whereApp = "whereis";
            }

            return Execute(whereApp, executableName);
        }

        public static bool Execute(
            string executable,
            string arguments = null,
            string workingDirectory = null,
            Action<string> stdOut = null,
            Action<string> stdErr = null,
            int timeout = DefaultProcessTimeout)
        {
            Process proc = null;

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = executable,
                    Arguments = arguments,

                    RedirectStandardOutput = true,
                    RedirectStandardError = true,

                    UseShellExecute = false,
                    
#if DNX451
                    WindowStyle = ProcessWindowStyle.Hidden,
#else
                    CreateNoWindow = true
#endif
                };

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    startInfo.WorkingDirectory = workingDirectory;
                }

                proc = new Process();
                proc.StartInfo = startInfo;
                proc.Start();

                proc.EnableRaisingEvents = true;

                if (stdOut != null)
                {
                    proc.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            stdOut(e.Data);
                        }
                    };
                    proc.BeginOutputReadLine();
                }

                if (stdErr != null)
                {
                    proc.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            stdErr(e.Data);
                        }
                    };
                    proc.BeginErrorReadLine();
                }

                proc.WaitForExit(timeout);

                return proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                if (stdErr != null)
                {
                    stdErr(ex.ToString());
                }
                return false;
            }
        }
    }
}
