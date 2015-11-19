// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishRoot
    {
        private readonly Runtime.Project _project;
        private LockFile _mainProjectLockFile;
        public static readonly string AppRootName = "approot";
        public static readonly string SourceFolderName = "src";

        public PublishRoot(Runtime.Project project, string outputPath, Reports reports)
        {
            _project = project;
            MainProjectName = project.Name;
            Reports = reports;
            Projects = new List<PublishProject>();
            Packages = new List<PublishPackage>();
            Runtimes = new List<PublishRuntime>();
            RuntimeIdentifiers = new HashSet<string>();
            Frameworks = new Dictionary<FrameworkName, string>();
            OutputPath = outputPath;
            TargetPackagesPath = Path.Combine(outputPath, AppRootName, "packages");
            TargetRuntimesPath = Path.Combine(outputPath, AppRootName, "runtimes");
            Operations = new PublishOperations();
        }

        public string OutputPath { get; private set; }
        public string TargetPackagesPath { get; private set; }
        public string TargetRuntimesPath { get; private set; }
        public string SourcePackagesPath { get; set; }
        public string MainProjectName { get; }

        public bool NoSource { get; set; }
        public bool IncludeSymbols { get; set; }
        public string Configuration { get; set; }

        public IList<PublishRuntime> Runtimes { get; set; }
        public ISet<string> RuntimeIdentifiers { get; set; }
        public IDictionary<FrameworkName, string> Frameworks { get; set; }
        public IList<PublishProject> Projects { get; private set; }
        public IList<PublishPackage> Packages { get; private set; }

        public Reports Reports { get; private set; }
        public PublishOperations Operations { get; private set; }

        // The lock file of the root project in the project directory
        public LockFile MainProjectLockFile
        {
            get
            {
                if (_project == null)
                {
                    return null;
                }

                var projectLockFilePath = Path.Combine(_project.ProjectDirectory, LockFileFormat.LockFileName);

                if (File.Exists(projectLockFilePath))
                {
                    _mainProjectLockFile = new LockFileFormat().Read(projectLockFilePath);
                }

                return _mainProjectLockFile;
            }
        }
        // The lock file of the root project in the publish output directory
        public LockFile PublishedLockFile { get; set; }

        public string IISCommand { get; set; }

        public bool Emit()
        {
            Reports.Information.WriteLine("Copying to output path {0}", OutputPath);

            var mainProject = Projects.Single(project => project.Library.Name == _project.Name);

            // Emit all package dependencies in parallel
            Parallel.ForEach(Packages, package => package.Emit(this));

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
                relativeAppBase = $@"packages\{_project.Name}\{_project.Version}\root";
            }
            else
            {
                relativeAppBase = $@"src\{_project.Name}";
            }

            foreach (var commandName in _project.Commands.Keys)
            {
                var runtimeFolder = string.Empty;
                if (Runtimes.Any())
                {
                    runtimeFolder = Runtimes.First().Name;
                }

                var cmdPath = Path.Combine(OutputPath, AppRootName, commandName + ".cmd");
                var cmdScript = $@"
@echo off
SET DNX_FOLDER={runtimeFolder}
SET ""LOCAL_DNX=%~dp0runtimes\%DNX_FOLDER%\bin\{Runtime.Constants.BootstrapperExeName}.exe""

IF EXIST %LOCAL_DNX% (
  SET ""DNX_PATH=%LOCAL_DNX%""
)

for %%a in (%DNX_HOME%) do (
    IF EXIST %%a\runtimes\%DNX_FOLDER%\bin\{Runtime.Constants.BootstrapperExeName}.exe (
        SET ""HOME_DNX=%%a\runtimes\%DNX_FOLDER%\bin\{Runtime.Constants.BootstrapperExeName}.exe""
        goto :continue
    )
)

:continue

IF ""%HOME_DNX%"" NEQ """" (
  SET ""DNX_PATH=%HOME_DNX%""
)

IF ""%DNX_PATH%"" == """" (
  SET ""DNX_PATH={Runtime.Constants.BootstrapperExeName}.exe""
)

@""%DNX_PATH%"" --project ""%~dp0{relativeAppBase}"" --configuration {Configuration} {commandName} %*
";
                File.WriteAllText(cmdPath, cmdScript);
            }
        }

        private void GenerateBashScripts()
        {
            string relativeAppBase;
            if (NoSource)
            {
                relativeAppBase = $"packages/{_project.Name}/{_project.Version}/root";
            }
            else
            {
                relativeAppBase = $"src/{_project.Name}";
            }

            foreach (var commandName in _project.Commands.Keys)
            {
                var runtimeFolder = string.Empty;
                if (Runtimes.Any())
                {
                    var runtime = Runtimes.First().Name;
                    runtimeFolder = $@"$DIR/runtimes/{runtime}/bin/";
                }

                var scriptPath = Path.Combine(OutputPath, AppRootName, commandName);
                var scriptContents = $@"#!/usr/bin/env bash

SOURCE=""${{BASH_SOURCE[0]}}""
while [ -h ""$SOURCE"" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""
  SOURCE=""$(readlink ""$SOURCE"")""
  [[ $SOURCE != /* ]] && SOURCE=""$DIR/$SOURCE"" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""

exec ""{runtimeFolder}{Runtime.Constants.BootstrapperExeName}"" --project ""$DIR/{relativeAppBase}"" --configuration {Configuration} {commandName} ""$@""";

                File.WriteAllText(scriptPath, scriptContents.Replace("\r\n", "\n"));

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

            // All dependency projects go to approot/src folder
            // so we remove all other useless entries that might bring in ambiguity
            rootObject["projects"] = new JArray(SourceFolderName);
            rootObject["packages"] = PathUtility.GetRelativePath(
                PathUtility.EnsureTrailingForwardSlash(applicationRoot),
                TargetPackagesPath, separator: '/');

            File.WriteAllText(Path.Combine(applicationRoot, GlobalSettings.GlobalFileName),
                rootObject.ToString());
        }
    }
}

