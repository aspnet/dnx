// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using kre.hosting;
using Microsoft.Framework.Runtime;

public class EntryPoint
{
    public static int Main(string[] arguments)
    {
        // Check for the debug flag before doing anything else
        bool hasDebugWaitFlag = arguments.Any(arg => string.Equals(arg, "--debug", StringComparison.OrdinalIgnoreCase));
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

        // Set the default lib path to be next to the entry point location
        Environment.SetEnvironmentVariable(EnvironmentNames.DefaultLib, Path.GetDirectoryName(typeof(EntryPoint).Assembly.Location));
        Environment.SetEnvironmentVariable(EnvironmentNames.ConsoleHost, "1");

        arguments = ExpandCommandLineArguments(arguments);

        // Set application base dir
        var appbaseIndex = arguments.ToList().FindIndex(arg =>
            string.Equals(arg, "--appbase", StringComparison.OrdinalIgnoreCase));
        if (appbaseIndex >= 0 && (appbaseIndex < arguments.Length - 1))
        {
            Environment.SetEnvironmentVariable(EnvironmentNames.AppBase, arguments[appbaseIndex + 1]);
        }

        return RuntimeBootstrapper.Execute(arguments);
    }
    
    private static string[] ExpandCommandLineArguments(string[] arguments)
    {
        // If '--appbase' is already given and it has a value, don't need to expand
        var appbaseIndex = arguments.ToList().FindIndex(arg =>
            string.Equals(arg, "--appbase", StringComparison.OrdinalIgnoreCase));
        if (appbaseIndex >= 0 && (appbaseIndex < arguments.Length - 1))
        {
            return arguments;
        }

        var expandedArgs = new List<string>();

        // Copy all arguments (options & values) as is before the project.json/assembly path
        var pathArgIndex = -1;
        while (++pathArgIndex < arguments.Length)
        {
            var optionValNum = BootstrapperOptionValueNum(arguments[pathArgIndex]);

            // It isn't a bootstrapper option, we treat it as the project.json/assembly path
            if (optionValNum < 0)
            {
                break;
            }

            // Copy the option
            expandedArgs.Add(arguments[pathArgIndex]);

            // Copy the value if the option has one
            if (optionValNum > 0 && (++pathArgIndex < arguments.Length))
            {
                expandedArgs.Add(arguments[pathArgIndex]);
            }
        }

        // No path argument was found, no expansion is needed
        if (pathArgIndex >= arguments.Length)
        {
            return arguments;
        }

        // Start to expand appbase option from path
        expandedArgs.Add("--appbase");

        var pathArg = arguments[pathArgIndex];
        if (pathArg.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            pathArg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            // "klr /path/App.dll arg1" --> "klr --appbase /path/ /path/App.dll arg1"
            // "klr /path/App.exe arg1" --> "klr --appbase /path/ /path/App.exe arg1"
            expandedArgs.Add(Path.GetDirectoryName(Path.GetFullPath(pathArg)));
            expandedArgs.AddRange(arguments.Skip(pathArgIndex));
        }
        else
        {
            var fileName = Path.GetFileName(pathArg);
            if (string.Equals(fileName, "project.json", StringComparison.OrdinalIgnoreCase))
            {
                // "klr /path/project.json run" --> "klr --appbase /path/ Microsoft.Framework.ApplicationHost run"
                expandedArgs.Add(Path.GetDirectoryName(Path.GetFullPath(pathArg)));
            }
            else
            {
                // "klr /path/ run" --> "klr --appbase /path/ Microsoft.Framework.ApplicationHost run"
                expandedArgs.Add(pathArg);
            }

            expandedArgs.Add("Microsoft.Framework.ApplicationHost");
            expandedArgs.AddRange(arguments.Skip(pathArgIndex + 1));
        }

        return expandedArgs.ToArray();
    }

    private static int BootstrapperOptionValueNum(string candidate)
    {
        if (string.Equals(candidate, "--appbase", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--lib", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--packages", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--configuration", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--port", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        else if (string.Equals(candidate, "--watch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--h", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--?", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "--version", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        // It isn't a bootstrapper option
        return -1;
    }
}