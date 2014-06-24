// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackRoot
    {
        private readonly Runtime.Project _project;

        public PackRoot(Runtime.Project project, string outputPath)
        {
            _project = project;
            Projects = new List<PackProject>();
            Packages = new List<PackPackage>();
            Runtimes = new List<PackRuntime>();
            OutputPath = outputPath;
            PackagesPath = Path.Combine(outputPath, "packages");
            Operations = new PackOperations();
        }

        public string OutputPath { get; private set; }
        public string PackagesPath { get; private set; }

        public string AppFolder { get; set; }
        public bool Overwrite { get; set; }
        public bool ZipPackages { get; set; }
        public bool NoSource { get; set; }

        public IList<PackRuntime> Runtimes { get; set; }
        public IList<PackProject> Projects { get; private set; }
        public IList<PackPackage> Packages { get; private set; }

        public PackOperations Operations { get; private set; }

        public void Emit()
        {
            Console.WriteLine("Copying to output path {0}", OutputPath);

            var mainProject = Projects.Single(project => project.Name == _project.Name);

            foreach (var deploymentPackage in Packages)
            {
                deploymentPackage.Emit(this);
            }

            foreach (var deploymentProject in Projects)
            {
                // TODO: temporarily we always emit sources for main project to make sure "k run"
                // can find entry point of the program. Later we should make main project
                // a nukpg too.
                if (deploymentProject == mainProject)
                {
                    deploymentProject.EmitSource(this);
                }
                else
                {
                    if (NoSource)
                    {
                        deploymentProject.EmitNupkg(this);
                    }
                    else
                    {
                        deploymentProject.EmitSource(this);
                    }
                }
            }

            foreach (var deploymentRuntime in Runtimes)
            {
                deploymentRuntime.Emit(this);
            }

            mainProject.PostProcess(this);

            foreach (var commandName in _project.Commands.Keys)
            {
                const string template1 = @"
@""%~dp0packages\{2}\bin\klr.exe"" --appbase ""%~dp0{1}"" Microsoft.Framework.ApplicationHost {0} %*
";
                const string template2 = @"
@klr.exe --appbase ""%~dp0{1}"" Microsoft.Framework.ApplicationHost {0} %*
";
                if (Runtimes.Any())
                {
                    File.WriteAllText(
                        Path.Combine(OutputPath, commandName + ".cmd"),
                        string.Format(template1, commandName, AppFolder ?? _project.Name, Runtimes.First().Name));
                }
                else
                {
                    File.WriteAllText(
                        Path.Combine(OutputPath, commandName + ".cmd"),
                        string.Format(template2, commandName, AppFolder ?? _project.Name));
                }
            }
        }
    }
}

