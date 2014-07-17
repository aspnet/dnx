// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.Packing
{
    public class PackRoot
    {
        private readonly Runtime.Project _project;
        private readonly IReport _report;
        public static readonly string AppRootName = "approot";

        public PackRoot(Runtime.Project project, string outputPath, IServiceProvider hostServices, IReport report)
        {
            _project = project;
            _report = report;
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

        public PackOperations Operations { get; private set; }

        public IServiceProvider HostServices { get; private set; }

        public void Emit()
        {
            _report.WriteLine("Copying to output path {0}", OutputPath);

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

            string relativeAppBase;
            if (NoSource)
            {
                relativeAppBase = Path.Combine(AppRootName, "packages", _project.Name,
                    _project.Version.ToString(), "root");
            }
            else
            {
                relativeAppBase = Path.Combine(AppRootName, "src", _project.Name);
            }

            foreach (var commandName in _project.Commands.Keys)
            {
                const string template1 = @"
@""%~dp0{3}\packages\{2}\bin\klr.exe"" --appbase ""%~dp0{1}"" Microsoft.Framework.ApplicationHost {0} %*
";
                const string template2 = @"
@klr.exe --appbase ""%~dp0{1}"" Microsoft.Framework.ApplicationHost {0} %*
";
                if (Runtimes.Any())
                {
                    File.WriteAllText(
                        Path.Combine(OutputPath, commandName + ".cmd"),
                        string.Format(template1, commandName, relativeAppBase, Runtimes.First().Name, AppRootName));
                }
                else
                {
                    File.WriteAllText(
                        Path.Combine(OutputPath, commandName + ".cmd"),
                        string.Format(template2, commandName, relativeAppBase));
                }
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
            rootObject["packages"] = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(applicationRoot),
                                                                 TargetPackagesPath);

            File.WriteAllText(Path.Combine(applicationRoot, GlobalSettings.GlobalFileName),
                rootObject.ToString());
        }
    }
}

