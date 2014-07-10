// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackRoot
    {
        private readonly Runtime.Project _project;
        public static readonly string AppRootName = "approot";

        public PackRoot(Runtime.Project project, string outputPath)
        {
            _project = project;
            Projects = new List<PackProject>();
            Packages = new List<PackPackage>();
            Runtimes = new List<PackRuntime>();
            OutputPath = outputPath;
            PackagesPath = Path.Combine(outputPath, AppRootName, "packages");
            Operations = new PackOperations();
        }

        public string OutputPath { get; private set; }
        public string PackagesPath { get; private set; }

        public string AppFolder { get; set; }
        public bool Overwrite { get; set; }
        public bool ZipPackages { get; set; }
        public bool NoSource { get; set; }
        public string Configuration { get; set; }

        public IList<PackRuntime> Runtimes { get; set; }
        public IList<PackProject> Projects { get; private set; }
        public IList<PackPackage> Packages { get; private set; }

        public PackOperations Operations { get; private set; }

        public void Emit()
        {
            Console.WriteLine("Copying to output path {0}", OutputPath);

            var mainProject = Projects.Single(project => project.Name == _project.Name);

            // TODO: this folder is needed by WebDeply team
            // we create an empty one as a temp workaround.
            // Later we should extract all static files from main project
            // and put them into this folder.
            var staticFileRootName = AppFolder ?? mainProject.Name;
            var staticFileRootPath = Path.Combine(OutputPath, staticFileRootName);
            Directory.CreateDirectory(staticFileRootPath);

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

            WriteDependenciesToGlobalJson();

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
                        string.Format(template1, commandName, Path.Combine(AppRootName, "src", AppFolder ?? _project.Name), Runtimes.First().Name));
                }
                else
                {
                    File.WriteAllText(
                        Path.Combine(OutputPath, commandName + ".cmd"),
                        string.Format(template2, commandName, Path.Combine(AppRootName, "src", AppFolder ?? _project.Name)));
                }
            }
        }

        private void WriteDependenciesToGlobalJson()
        {
            var nugetDependencyResolver = new NuGetDependencyResolver(
                _project.ProjectDirectory,
                PackagesPath,
                new EmptyFrameworkResolver());
            var rootDirectory = ProjectResolver.ResolveRootDirectory(_project.ProjectDirectory);
            var projectResolver = new ProjectResolver(_project.ProjectDirectory, rootDirectory);
            var dependenciesObj = new JObject();

            // Generate SHAs for all package dependencies
            foreach (var deploymentPackage in Packages)
            {
                // Use the exactly same approach in PackPackage.Emit() to
                // find the package actually in use
                var package = nugetDependencyResolver.FindCandidate(
                    deploymentPackage.Library.Name,
                    deploymentPackage.Library.Version);
                var shaFilePath = Path.Combine(
                    PackagesPath,
                    string.Format("{0}.{1}", package.Id, package.Version),
                    string.Format("{0}.{1}.nupkg.sha512", package.Id, package.Version));
                var sha = File.ReadAllText(shaFilePath);

                var shaObj = new JObject();
                shaObj.Add(new JProperty("version", package.Version.ToString()));
                shaObj.Add(new JProperty("sha", sha));
                dependenciesObj.Add(new JProperty(package.Id, shaObj));
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

                var shaFilePath = Path.Combine(
                    PackagesPath,
                    string.Format("{0}.{1}", project.Name, project.Version),
                    string.Format("{0}.{1}.nupkg.sha512", project.Name, project.Version));

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
            var rootDir = ProjectResolver.ResolveRootDirectory(_project.ProjectDirectory);
            if (GlobalSettings.HasGlobalFile(rootDir))
            {
                rootObject = JObject.Parse(File.ReadAllText(Path.Combine(
                    rootDir,
                    GlobalSettings.GlobalFileName)));
            }
            else
            {
                rootObject = new JObject();
            }

            rootObject["dependencies"] = dependenciesObj;

            File.WriteAllText(Path.Combine(OutputPath, PackRoot.AppRootName, GlobalSettings.GlobalFileName),
                rootObject.ToString());
        }
    }
}

