// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.DependencyAnalyzer
{
    internal class BuildRuntimeCommand
    {
        private const string KeyRuntime = "Runtime";

        private readonly IEnumerable<string> _runtimeProjects;
        private readonly IEnumerable<string> _sdkProjects;

        public BuildRuntimeCommand()
        {
            _runtimeProjects = new[]
            {
                "Microsoft.Framework.Runtime.Roslyn",
                "Microsoft.Framework.ApplicationHost",
                "klr.host",
                "klr.core45.managed"
            };

            _sdkProjects = new[]
            {
                "Microsoft.Framework.DesignTimeHost",
                "Microsoft.Framework.PackageManager",
                "Microsoft.Framework.Project"
            };
        }

        public string CoreClrRoot { get; set; }

        public string Output { get; set; }

        public Reports Reports { get; set; }

        public string KreRoot { get; internal set; }

        public int Execute()
        {
            bool isCLR = CoreClrRoot == null;

            var finder = GetDependencyFinder(isCLR);
            if (finder == null)
            {
                return 1;
            }

            var dependencies = new Dictionary<string, HashSet<string>>();

            dependencies[KeyRuntime] = new HashSet<string>();

            foreach (var name in _runtimeProjects)
            {
                dependencies[KeyRuntime].AddRange(finder.GetDependencies(name));
            }

            foreach (var name in _sdkProjects)
            {
                dependencies[name] = finder.GetDependencies(name);
            }

            foreach (var pair in dependencies.Skip(1))
            {
                pair.Value.ExceptWith(dependencies[KeyRuntime]);
            }

            var content = new List<string>();
            foreach (var root in dependencies)
            {
                content.Add("-" + root.Key);
                foreach (var contract in root.Value)
                {
                    content.Add(contract);
                }
            }

            if (Output == null)
            {
                content.ForEach(line => Reports.Information.WriteLine(line));
            }
            else
            {
                using (var sw = File.CreateText(Output))
                {
                    content.ForEach(line => sw.WriteLine(line));
                }
            }

            return 0;
        }

        private DependencyFinder GetDependencyFinder(bool isCLR)
        {
            if (string.IsNullOrEmpty(KreRoot) || !Directory.Exists(KreRoot))
            {
                Reports.Error.WriteLine("A valid path to the KRE folder is required");
                return null;
            }

            if (isCLR)
            {
                Reports.Verbose.WriteLine("Using dependency finder for CLR");
                return new DependencyFinder(KreRoot, VersionUtility.ParseFrameworkName("aspnet50"),
                    hostContext => new DependencyResolverForCLR(hostContext));
            }
            else
            {
                if (string.IsNullOrEmpty(CoreClrRoot) || !Directory.Exists(CoreClrRoot))
                {
                    Reports.Error.WriteLine("A valid path to the CoreCLR folder is required when target framework is CoreCLR.");
                    return null;
                }

                Reports.Verbose.WriteLine("Using dependency finder for CoreCLR");
                return new DependencyFinder(KreRoot, VersionUtility.ParseFrameworkName("aspnetcore50"),
                    hostContext => new DependencyResolverForCoreCLR(hostContext, CoreClrRoot));
            }
        }
    }
}