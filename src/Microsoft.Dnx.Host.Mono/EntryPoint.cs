// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Dnx.Host;
using Microsoft.Dnx.Runtime;

public class EntryPoint
{
    public static int Main(string[] arguments)
    {
        // Check for the debug flag before doing anything else
        bool hasDebugWaitFlag = false;
        for (var i = 0; i < arguments.Length; ++i)
        {
            //anything without - or -- is appbase or non-dnx command
            if (arguments[i][0] != '-')
            {
                break;
            }
            if (arguments[i] == "--appbase")
            {
                //skip path argument
                ++i;
                continue;
            }
            if (arguments[i] == "--debug")
            {
                hasDebugWaitFlag = true;
                break;
            }
        }

        if (hasDebugWaitFlag)
        {
            if (!Debugger.IsAttached)
            {
                Process currentProc = Process.GetCurrentProcess();
                Console.WriteLine("Process Id: {0}", currentProc.Id);
                Console.WriteLine("Waiting for the debugger to attach...");

                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(250);
                }

                Console.WriteLine("Debugger attached.");
            }
        }

        arguments = ExpandCommandLineArguments(arguments);

        // Set application base dir
        var appbaseIndex = arguments.ToList().FindIndex(arg =>
            string.Equals(arg, "--appbase", StringComparison.OrdinalIgnoreCase));

        var appBase = appbaseIndex >= 0 && (appbaseIndex < arguments.Length - 1)
                ? arguments[appbaseIndex + 1]
                : Directory.GetCurrentDirectory();

        string operatingSystem, osVersion, architecture;
        GetOsDetails(out operatingSystem, out osVersion, out architecture);

        var bootstrapperContext = new BootstrapperContext
        {
            OperatingSystem = operatingSystem,
            OsVersion = osVersion,
            Architecture = architecture,
            RuntimeDirectory = Path.GetDirectoryName(typeof(EntryPoint).Assembly.Location),
            ApplicationBase = appBase,
            // NOTE(anurse): Mono is always "dnx451" (for now).
            TargetFramework = new FrameworkName("DNX", new Version(4, 5, 1)),
            HandleExceptions = true
        };

        return RuntimeBootstrapper.Execute(arguments, bootstrapperContext);
    }

    private static string[] ExpandCommandLineArguments(string[] arguments)
    {
        var parameterIdx = FindAppBaseOrNonHostOption(arguments);

        // no non-bootstrapper parameters found or --appbase was found
        if (parameterIdx < 0 || string.Equals(arguments[parameterIdx], "--appbase", StringComparison.OrdinalIgnoreCase))
        {
            return arguments;
        }

        var argExpanded = false;
        var expandedArgs = new List<string>();
        for (var i = 0; i < arguments.Count(); i++)
        {
            if (!argExpanded)
            {
                if (string.Equals(arguments[i], "-p", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arguments[i], "--project", StringComparison.OrdinalIgnoreCase))
                {
                    // Note that ++i is safe here since if we had a trailing -p/--project we would have exited
                    // before entering the loop because we wouldn't have found any non-host option
                    ExpandProject(arguments[++i], expandedArgs);
                    argExpanded = true;
                }
                else if (i == parameterIdx)
                {
                    ExpandNonHostArgument(arguments[i], expandedArgs);
                    argExpanded = true;
                }
                else
                {
                    expandedArgs.Add(arguments[i]);
                }
            }
            else
            {
                expandedArgs.Add(arguments[i]);
            }
        }

        return expandedArgs.ToArray();
    }

    private static void ExpandProject(string projectPath, List<string> expandedArgs)
    {
        expandedArgs.Add("--appbase");
        if (string.Equals(Path.GetFileName(projectPath), "project.json", StringComparison.OrdinalIgnoreCase))
        {
            expandedArgs.Add(Path.GetDirectoryName(Path.GetFullPath(projectPath)));
        }
        else
        {
            expandedArgs.Add(Path.GetFullPath(projectPath));
        }

        expandedArgs.Add("Microsoft.Dnx.ApplicationHost");
    }

    private static void ExpandNonHostArgument(string argument, List<string> expandedArgs)
    {
        expandedArgs.Add("--appbase");

        if (argument.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            argument.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            // "dnx /path/App.dll arg1" --> "dnx --appbase /path/ /path/App.dll arg1"
            // "dnx /path/App.exe arg1" --> "dnx --appbase /path/ /path/App.exe arg1"
            expandedArgs.Add(Path.GetDirectoryName(Path.GetFullPath(argument)));
            expandedArgs.Add(argument);

            return;
        }

        // "dnx run" --> "dnx --appbase . Microsoft.Dnx.ApplicationHost run"
        expandedArgs.Add(".");
        expandedArgs.Add("Microsoft.Dnx.ApplicationHost");
        expandedArgs.Add(argument);
    }

    private static int FindAppBaseOrNonHostOption(string[] arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            if (string.Equals(arguments[i], "--appbase", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }

            var option_arg_count = BootstrapperOptionValueNum(arguments[i]);
            if (option_arg_count < 0)
            {
                return i;
            }

            i += option_arg_count;
        }

        return -1;
    }

    private static int BootstrapperOptionValueNum(string candidate)
    {
        if (string.Equals(candidate, "--appbase", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--lib", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--packages", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--configuration", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--port", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--project", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "-p", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        else if (string.Equals(candidate, "--watch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--debug", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "-h", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "-?", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--version", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        // It isn't a bootstrapper option
        return -1;
    }

    private static void GetOsDetails(out string operatingSystem, out string osVersion, out string architecture)
    {
        var unameOutput = RunProgram("uname", "-s -m").Split(' ');

        operatingSystem = unameOutput[0];
        architecture = TranslateArchitecture(unameOutput[1]);
        if (operatingSystem == "Darwin")
        {
            // sw_vers returns versions in format "10.10.4" so ".4" needs to be removed
            osVersion = RunProgram("sw_vers", "-productVersion");
            var lastDot = osVersion.LastIndexOf('.');
            osVersion = lastDot >= 0 ? osVersion.Substring(0, lastDot) : osVersion;
            return;
        }

        try
        {
            var lsb_release = RunProgram("lsb_release", "-s -i -r");
            if (!string.IsNullOrEmpty(lsb_release))
            {
                osVersion = lsb_release.Replace(Environment.NewLine, " ");
                return;
            }
        }
        catch
        {
        }

        try
        {
            // Red Hat based Linux without lsb_release
            var redHatRelease = File.ReadAllText("/etc/redhat-release");
            var m = Regex.Match(redHatRelease, @"^(?<distro>\S+) \S+ \S+ (?<version>\d+\.\d+)\..+$");
            if (m.Success)
            {
                osVersion = string.Format("{0} {1}", m.Groups["distro"], m.Groups["version"]);
                return;
            }
        }
        catch
        {
        }

        Console.WriteLine("Could determine OS version information. Defaulting to the empty string.");
        osVersion = string.Empty;
    }

    private static string TranslateArchitecture(string architecture)
    {
        if (architecture == "x86_64")
        {
            return "x64";
        }

        if (architecture.StartsWith("armv7"))
        {
            return "arm";
        }

        return "x86";
    }

    private static string RunProgram(string name, string args)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = name,
            Arguments = args,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        var process = Process.Start(processStartInfo);
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }
}
