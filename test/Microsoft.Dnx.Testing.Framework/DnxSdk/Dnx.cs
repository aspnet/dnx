// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Testing.Framework
{
    public class Dnx
    {
        private readonly string _sdkPath;

        public Dnx(string sdkPath)
        {
            _sdkPath = sdkPath;
        }

        public ExecResult Execute(Project project, string commandLine = null, bool dnxTraceOn = false)
        {
            return Execute($"-p \"{project.ProjectDirectory}\" {commandLine ?? "run"}", dnxTraceOn);
        }

        public ExecResult Execute(string commandLine, bool dnxTraceOn = false, Action<Dictionary<string, string>> envSetup = null)
        {
            string command;
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                command = "cmd";
                commandLine = $"/C \"\"{Path.Combine(_sdkPath, "bin", "dnx.exe")}\" {commandLine}\"";
            }
            else
            {
                command = Path.Combine(_sdkPath, "bin", "dnx");
            }
            return Exec.Run(
                command,
                commandLine,
                env =>
                {
                    env[EnvironmentNames.Trace] = dnxTraceOn ? "1" : null;
                    envSetup?.Invoke(env);
                });
        }
    }
}
