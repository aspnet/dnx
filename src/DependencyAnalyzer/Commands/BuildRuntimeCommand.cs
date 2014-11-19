// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DependencyAnalyzer.Util;
using Microsoft.Framework.Runtime;
using NuGet;

namespace DependencyAnalyzer.Commands
{
    public class BuildRuntimeCommand
    {
        private const string KeyRuntime = "Runtime";

        private readonly IApplicationEnvironment _environment;
        private readonly IEnumerable<string> _runtimeProjects;
        private readonly IEnumerable<string> _sdkProjects;
        private readonly string _assemblyFolder;
        private readonly string _outputFile;

        public BuildRuntimeCommand(IApplicationEnvironment env, string assemblyFolder, string outputFile)
        {
            _environment = env;

            // TODO: Unhard coded this?
            _runtimeProjects = new[]
            {
                "Microsoft.Framework.Runtime.Roslyn",
                "Microsoft.Framework.ApplicationHost",
                "klr.host",
                "klr.core45.managed"
            };

            // TODO: Unhard coded this?
            _sdkProjects = new[]
            {
                "Microsoft.Framework.DesignTimeHost",
                "Microsoft.Framework.PackageManager",
                "Microsoft.Framework.Project"
            };

            _assemblyFolder = assemblyFolder;
            _outputFile = outputFile;
        }

        public int Execute()
        {
            var accessor = new CacheContextAccessor();
            var cache = new Cache(accessor);
            var finder = new DependencyFinder(accessor, cache, _environment, _assemblyFolder);

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

            using (var output = _outputFile != null ? new StreamWriter(_outputFile) : Console.Out)
            {
                foreach (var root in dependencies)
                {
                    output.WriteLine("-" + root.Key);
                    foreach (var contract in root.Value)
                    {
                        output.WriteLine(contract);
                    }
                }

                output.Write("-");
                output.Flush();
            }

            return 0;
        }
    }
}