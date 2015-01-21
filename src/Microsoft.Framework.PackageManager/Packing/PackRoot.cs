// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackRoot
    {
        private readonly Runtime.Project _project;
        public static readonly string AppRootName = "approot";

        public PackRoot(Runtime.Project project, string outputPath, IServiceProvider hostServices, Reports reports)
        {
            _project = project;
            Reports = reports;
            Projects = new List<PackProject>();
            Packages = new List<PackPackage>();
            Runtimes = new List<PackRuntime>();
            OutputPath = outputPath;
            HostServices = hostServices;
            TargetPackagesPath = Path.Combine(outputPath, AppRootName, "packages");
            Operations = new PackOperations();
            LibraryDependencyContexts = new Dictionary<Library, IList<DependencyContext>>();
        }

        public string OutputPath { get; private set; }
        public string TargetPackagesPath { get; private set; }
        public string SourcePackagesPath { get; set; }

        public bool Overwrite { get; set; }
        public bool NoSource { get; set; }
        public string Configuration { get; set; }

        public IList<PackRuntime> Runtimes { get; set; }
        public IList<PackProject> Projects { get; private set; }
        public IList<PackPackage> Packages { get; private set; }
        public IDictionary<Library, IList<DependencyContext>> LibraryDependencyContexts { get; private set; }

        public Reports Reports { get; private set; }
        public PackOperations Operations { get; private set; }

        public IServiceProvider HostServices { get; private set; }

        public void Emit()
        {
            Reports.Quiet.WriteLine("Copying to output path {0}", OutputPath);

            var mainProject = Projects.Single(project => project.Name == _project.Name);

            foreach (var deploymentPackage in Packages)
            {
                deploymentPackage.Emit(this);
            }

            foreach (var deploymentProject in Projects)
            {
                deploymentProject.Emit(this);
            }

            foreach (var deploymentRuntime in Runtimes)
            {
                deploymentRuntime.Emit(this);
            }

            mainProject.PostProcess(this);

            WriteGlobalJson();

            // Generate .cmd files
            GenerateBatchFiles();

            // Generate executables (bash scripts without .sh extension) for *nix
            GenerateBashScripts();
        }

        private void GenerateBatchFiles()
        {
            string relativeAppBase;
            if (NoSource)
            {
                relativeAppBase = string.Format(@"{0}\{1}\{2}\{3}\{4}",
                    AppRootName,
                    "packages",
                    _project.Name,
                    _project.Version,
                    "root");
            }
            else
            {
                relativeAppBase = string.Format(@"{0}\{1}\{2}", AppRootName, "src", _project.Name);
            }

            const string template = @"
@""{0}dotnet.exe"" --appbase ""%~dp0{1}"" Microsoft.Framework.ApplicationHost {2} %*
";

            foreach (var commandName in _project.Commands.Keys)
            {
                var runtimeFolder = string.Empty;
                if (Runtimes.Any())
                {
                    runtimeFolder = string.Format(@"%~dp0{0}\packages\{1}\bin\", AppRootName, Runtimes.First().Name);
                }

                File.WriteAllText(
                    Path.Combine(OutputPath, commandName + ".cmd"),
                    string.Format(template, runtimeFolder, relativeAppBase, commandName));
            }
        }

        private void GenerateBashScripts()
        {
            string relativeAppBase;
            if (NoSource)
            {
                relativeAppBase = string.Format("{0}/{1}/{2}/{3}/{4}", AppRootName, "packages", _project.Name,
                    _project.Version.ToString(), "root");
            }
            else
            {
                relativeAppBase = string.Format("{0}/{1}/{2}", AppRootName, "src", _project.Name);
            }

            const string template = @"#!/bin/bash

SOURCE=""${{BASH_SOURCE[0]}}""
while [ -h ""$SOURCE"" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""
  SOURCE=""$(readlink ""$SOURCE"")""
  [[ $SOURCE != /* ]] && SOURCE=""$DIR/$SOURCE"" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""

export SET DOTNET_APPBASE=""$DIR/{0}""

exec ""{1}dotnet"" --appbase ""$DOTNET_APPBASE"" Microsoft.Framework.ApplicationHost {2} ""$@""";

            foreach (var commandName in _project.Commands.Keys)
            {
                var runtimeFolder = string.Empty;
                if (Runtimes.Any())
                {
                    runtimeFolder = string.Format(@"$DIR/{0}/packages/{1}/bin/",
                        AppRootName, Runtimes.First().Name);
                }

                var scriptPath = Path.Combine(OutputPath, commandName);
                File.WriteAllText(scriptPath,
                    string.Format(template, relativeAppBase, runtimeFolder, commandName).Replace("\r\n", "\n"));
                if (PlatformHelper.IsMono)
                {
                    MarkExecutable(scriptPath);
                }
            }
        }

        private void MarkExecutable(string scriptPath)
        {
            var processStartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                FileName = "chmod",
                Arguments = "+x " + scriptPath
            };

            var process = Process.Start(processStartInfo);
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Reports.Information.WriteLine("Failed to mark {0} as executable".Yellow(), scriptPath);
            }
        }

        private void WriteGlobalJson()
        {
            var rootDirectory = ProjectResolver.ResolveRootDirectory(_project.ProjectDirectory);
            var projectResolver = new ProjectResolver(_project.ProjectDirectory, rootDirectory);
            var packagesDir = NuGetDependencyResolver.ResolveRepositoryPath(rootDirectory);
            var pathResolver = new DefaultPackagePathResolver(packagesDir);
            var dependenciesObj = new JObject();

            // Generate SHAs for all package dependencies
            foreach (var deploymentPackage in Packages)
            {
                var library = deploymentPackage.Library;
                var shaFilePath = pathResolver.GetHashPath(library.Name, library.Version);

                if (!File.Exists(shaFilePath))
                {
                    throw new FileNotFoundException("Expected SHA file doesn't exist", shaFilePath);
                }

                var sha = File.ReadAllText(shaFilePath);

                var shaObj = new JObject();
                shaObj["version"] = library.Version.ToString();
                shaObj["sha"] = sha;
                dependenciesObj[library.Name] = shaObj;
            }

            // If "--no-source" is specified, project dependencies are packed to packages
            // So we also generate SHAs for them in this case
            foreach (var deploymentProject in Projects)
            {
                Runtime.Project project;
                if (!projectResolver.TryResolveProject(deploymentProject.Name, out project))
                {
                    throw new Exception("TODO: unable to resolve project named " + deploymentProject.Name);
                }

                var shaFilePath = pathResolver.GetHashPath(project.Name, project.Version);

                if (!File.Exists(shaFilePath))
                {
                    // This project is not packed to a package
                    continue;
                }

                var sha = File.ReadAllText(shaFilePath);

                var shaObj = new JObject();
                shaObj.Add(new JProperty("version", project.Version.ToString()));
                shaObj.Add(new JProperty("sha", sha));
                dependenciesObj.Add(new JProperty(project.Name, shaObj));
            }

            var rootObject = default(JObject);
            if (GlobalSettings.HasGlobalFile(rootDirectory))
            {
                rootObject = JObject.Parse(File.ReadAllText(Path.Combine(
                    rootDirectory,
                    GlobalSettings.GlobalFileName)));
            }
            else
            {
                rootObject = new JObject();
            }

            var applicationRoot = Path.Combine(OutputPath, PackRoot.AppRootName);

            rootObject["dependencies"] = dependenciesObj;
            rootObject["packages"] = PathUtility.GetRelativePath(
                PathUtility.EnsureTrailingForwardSlash(applicationRoot),
                TargetPackagesPath, separator: '/');

            File.WriteAllText(Path.Combine(applicationRoot, GlobalSettings.GlobalFileName),
                rootObject.ToString());
        }
    }
}

