// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishRoot
    {
        private readonly Runtime.Project _project;
        public static readonly string AppRootName = "approot";

        public PublishRoot(Runtime.Project project, string outputPath, IServiceProvider hostServices, Reports reports)
        {
            _project = project;
            Reports = reports;
            Projects = new List<PublishProject>();
            Packages = new List<PublishPackage>();
            Runtimes = new List<PublishRuntime>();
            OutputPath = outputPath;
            HostServices = hostServices;
            TargetPackagesPath = Path.Combine(outputPath, AppRootName, "packages");
            TargetRuntimesPath = Path.Combine(outputPath, AppRootName, "runtimes");
            Operations = new PublishOperations();
            LibraryDependencyContexts = new Dictionary<LibraryIdentity, IList<DependencyContext>>();
        }

        public string OutputPath { get; private set; }
        public string TargetPackagesPath { get; private set; }
        public string TargetRuntimesPath { get; private set; }
        public string SourcePackagesPath { get; set; }

        public bool NoSource { get; set; }
        public bool IncludeSymbols { get; set; }
        public string Configuration { get; set; }

        public IList<PublishRuntime> Runtimes { get; set; }
        public IList<PublishProject> Projects { get; private set; }
        public IList<PublishPackage> Packages { get; private set; }
        public IDictionary<LibraryIdentity, IList<DependencyContext>> LibraryDependencyContexts { get; private set; }

        public Reports Reports { get; private set; }
        public PublishOperations Operations { get; private set; }

        public IServiceProvider HostServices { get; private set; }

        public bool Emit()
        {
            Reports.Information.WriteLine("Copying to output path {0}", OutputPath);

            var mainProject = Projects.Single(project => project.Name == _project.Name);

            foreach (var deploymentPackage in Packages)
            {
                deploymentPackage.Emit(this);
            }

            var success = true;

            foreach (var deploymentProject in Projects)
            {
                success &= deploymentProject.Emit(this);
            }

            foreach (var deploymentRuntime in Runtimes)
            {
                success &= deploymentRuntime.Emit(this);
            }

            // Order matters here, we write out the global.json first
            // so that post process can find things
            WriteGlobalJson();

            success &= mainProject.PostProcess(this);

            // Generate .cmd files
            GenerateBatchFiles();

            // Generate executables (bash scripts without .sh extension) for *nix
            GenerateBashScripts();

            return success;
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
@""{0}{1}.exe"" --appbase ""%~dp0{2}"" Microsoft.Dnx.ApplicationHost --configuration {3} {4} %*
";

            foreach (var commandName in _project.Commands.Keys)
            {
                var runtimeFolder = string.Empty;
                if (Runtimes.Any())
                {
                    runtimeFolder = string.Format(@"%~dp0{0}\runtimes\{1}\bin\", AppRootName, Runtimes.First().Name);
                }

                File.WriteAllText(
                    Path.Combine(OutputPath, commandName + ".cmd"),
                    string.Format(template, 
                                  runtimeFolder,
                                  Runtime.Constants.BootstrapperExeName,
                                  relativeAppBase,
                                  Configuration,
                                  commandName));
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

            const string template = @"#!/usr/bin/env bash

SOURCE=""${{BASH_SOURCE[0]}}""
while [ -h ""$SOURCE"" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""
  SOURCE=""$(readlink ""$SOURCE"")""
  [[ $SOURCE != /* ]] && SOURCE=""$DIR/$SOURCE"" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""

export SET {0}=""$DIR/{1}""

exec ""{2}{3}"" --appbase ""${0}"" Microsoft.Dnx.ApplicationHost --configuration {4} {5} ""$@""";

            foreach (var commandName in _project.Commands.Keys)
            {
                var runtimeFolder = string.Empty;
                if (Runtimes.Any())
                {
                    runtimeFolder = string.Format(@"$DIR/{0}/runtimes/{1}/bin/",
                        AppRootName, Runtimes.First().Name);
                }

                var scriptPath = Path.Combine(OutputPath, commandName);
                File.WriteAllText(scriptPath,
                    string.Format(template, 
                                  EnvironmentNames.AppBase,
                                  relativeAppBase,
                                  runtimeFolder,
                                  Runtime.Constants.BootstrapperExeName, 
                                  Configuration,
                                  commandName).Replace("\r\n", "\n"));

                if (!RuntimeEnvironmentHelper.IsWindows)
                {
                    if (!FileOperationUtils.MarkExecutable(scriptPath))
                    {
                        Reports.Information.WriteLine("Failed to mark {0} as executable".Yellow(), scriptPath);
                    }
                }
            }
        }

        private void WriteGlobalJson()
        {
            var rootDirectory = ProjectResolver.ResolveRootDirectory(_project.ProjectDirectory);

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

            var applicationRoot = Path.Combine(OutputPath, PublishRoot.AppRootName);

            rootObject["packages"] = PathUtility.GetRelativePath(
                PathUtility.EnsureTrailingForwardSlash(applicationRoot),
                TargetPackagesPath, separator: '/');

            File.WriteAllText(Path.Combine(applicationRoot, GlobalSettings.GlobalFileName),
                rootObject.ToString());
        }
    }
}

