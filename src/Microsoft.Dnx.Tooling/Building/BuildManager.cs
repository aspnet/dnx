// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Compilation.FileSystem;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Tooling.SourceControl;
using Microsoft.Dnx.Tooling.Utils;
using Microsoft.Framework.FileSystemGlobbing;
using Microsoft.Framework.FileSystemGlobbing.Abstractions;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public class BuildManager
    {
        private readonly IServiceProvider _hostServices;
        private readonly IApplicationEnvironment _applicationEnvironment;
        private readonly BuildOptions _buildOptions;

        // Shared by all projects that will be built by this class
        private CompilationEngine _compilationEngine;

        private Runtime.Project _currentProject;

        public BuildManager(IServiceProvider hostServices, BuildOptions buildOptions)
        {
            _hostServices = hostServices;
            _buildOptions = buildOptions;

            _applicationEnvironment = (IApplicationEnvironment)hostServices.GetService(typeof(IApplicationEnvironment));
            var loadContextAccessor = (IAssemblyLoadContextAccessor)hostServices.GetService(typeof(IAssemblyLoadContextAccessor));

            _compilationEngine = new CompilationEngine(new CompilationEngineContext(_applicationEnvironment, loadContextAccessor.Default, new CompilationCache()));

            ScriptExecutor = new ScriptExecutor(buildOptions.Reports.Information);
        }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public bool Build()
        {
            var projectFilesFinder = new Matcher();

            // Resolve all the project names
            var projectFilesToBuild = new List<string>();
            foreach(var pattern in _buildOptions.ProjectPatterns)
            {
                if (pattern.Contains("*"))
                {
                    // Requires globbing
                    projectFilesFinder.AddInclude(NormalizeGlobbingPattern(pattern));
                }
                else
                {
                    projectFilesToBuild.Add(pattern);
                }
            }

            var rootDirectory = Directory.GetCurrentDirectory();
            var patternSearchFolder = new DirectoryInfoWrapper(new DirectoryInfo(rootDirectory));
            var globbingProjects = projectFilesFinder.Execute(patternSearchFolder).Files.Select(file =>
                Path.Combine(rootDirectory, file));
            projectFilesToBuild.AddRange(globbingProjects);

            var sw = Stopwatch.StartNew();

            var globalSucess = true;
            foreach (var project in projectFilesToBuild)
            {
                var buildSuccess = BuildInternal(project);
                globalSucess &= buildSuccess;
            }

            _buildOptions.Reports.Information.WriteLine($"Total build time elapsed: { sw.Elapsed }");
            _buildOptions.Reports.Information.WriteLine($"Total projects built: { projectFilesToBuild.Count }");

            return globalSucess;
        }

        private static string NormalizeGlobbingPattern(string pattern)
        {
            if (!string.Equals(Path.GetFileName(pattern), Runtime.Project.ProjectFileName, StringComparison.OrdinalIgnoreCase))
            {
                return pattern + "/" + Runtime.Project.ProjectFileName;
            }

            return pattern;
        }

        private bool BuildInternal(string projectPath)
        {
            var projectDiagnostics = new List<DiagnosticMessage>();

            if (!Runtime.Project.TryGetProject(projectPath, out _currentProject, projectDiagnostics))
            {
                LogError(string.Format("Unable to locate {0}.", Runtime.Project.ProjectFileName));
                return false;
            }

            var sw = Stopwatch.StartNew();

            var baseOutputPath = GetBuildOutputDir(_currentProject);
            var configurations = _buildOptions.Configurations.DefaultIfEmpty("Debug");

            string frameworkSelectionError;
            var frameworks = FrameworkSelectionHelper.SelectFrameworks(_currentProject,
                                                                       _buildOptions.TargetFrameworks,
                                                                       _applicationEnvironment.RuntimeFramework,
                                                                       out frameworkSelectionError);

            if (frameworks == null)
            {
                LogError(frameworkSelectionError);
                return false;
            }

            if (_buildOptions.GeneratePackages &&
                !ScriptExecutor.Execute(_currentProject, "prepack", GetScriptVariable))
            {
                LogError(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (!ScriptExecutor.Execute(_currentProject, "prebuild", GetScriptVariable))
            {
                LogError(ScriptExecutor.ErrorMessage);
                return false;
            }

            var success = true;

            var allDiagnostics = new List<DiagnosticMessage>();


            PackageBuilder packageBuilder = null;
            PackageBuilder symbolPackageBuilder = null;
            InstallBuilder installBuilder = null;
            SourceBuilder sourceBuilder = null;

            // Build all specified configurations
            foreach (var configuration in configurations)
            {
                if (_buildOptions.GeneratePackages)
                {
                    // Create a new builder per configuration
                    packageBuilder = new PackageBuilder();
                    symbolPackageBuilder = new PackageBuilder();
                    InitializeBuilder(_currentProject, packageBuilder);
                    InitializeBuilder(_currentProject, symbolPackageBuilder);
                    installBuilder = new InstallBuilder(_currentProject, packageBuilder, _buildOptions.Reports);
                    sourceBuilder = new SourceBuilder(_currentProject, packageBuilder, _buildOptions.Reports);
                }

                var configurationSuccess = true;

                var outputPath = Path.Combine(baseOutputPath, configuration);

                // Build all target frameworks a project supports
                foreach (var targetFramework in frameworks)
                {
                    _buildOptions.Reports.Information.WriteLine();
                    _buildOptions.Reports.Information.WriteLine("Building {0} for {1}",
                        _currentProject.Name, targetFramework.ToString().Yellow().Bold());

                    var diagnostics = new List<DiagnosticMessage>();

                    var context = new BuildContext(_compilationEngine,
                                                   _currentProject,
                                                   targetFramework,
                                                   configuration,
                                                   outputPath);

                    context.Initialize(_buildOptions.Reports.Quiet);

                    if (context.Build(diagnostics))
                    {
                        if (_buildOptions.GeneratePackages)
                        {
                            context.PopulateDependencies(packageBuilder);
                            context.AddLibs(packageBuilder, "*.dll");
                            context.AddLibs(packageBuilder, "*.xml");

                            context.PopulateDependencies(symbolPackageBuilder);
                            context.AddLibs(symbolPackageBuilder, "*.*");
                        }
                    }
                    else
                    {
                        configurationSuccess = false;
                    }

                    allDiagnostics.AddRange(diagnostics);

                    WriteDiagnostics(diagnostics);
                }

                success = success && configurationSuccess;

                if (_buildOptions.GeneratePackages)
                {
                    // Create a package per configuration
                    string nupkg = GetPackagePath(_currentProject, outputPath);
                    string symbolsNupkg = GetPackagePath(_currentProject, outputPath, symbols: true);

                    if (configurationSuccess)
                    {
                        // Generates the application package only if this is an application packages
                        configurationSuccess = installBuilder.Build(outputPath);
                        success = success && configurationSuccess;
                    }

                    if (configurationSuccess)
                    {
                        configurationSuccess = sourceBuilder.Build(outputPath);
                        success = success && configurationSuccess;
                    }

                    if (configurationSuccess)
                    {
                        foreach (var sharedFile in _currentProject.Files.SharedFiles)
                        {
                            var file = new PhysicalPackageFile();
                            file.SourcePath = sharedFile;
                            file.TargetPath = String.Format(@"shared\{0}", Path.GetFileName(sharedFile));
                            packageBuilder.Files.Add(file);
                        }

                        var root = _currentProject.ProjectDirectory;

                        foreach (var path in _currentProject.Files.SourceFiles)
                        {
                            var srcFile = new PhysicalPackageFile();
                            srcFile.SourcePath = path;
                            srcFile.TargetPath = Path.Combine("src", PathUtility.GetRelativePath(root, path));
                            symbolPackageBuilder.Files.Add(srcFile);
                        }

                        using (var fs = File.Create(nupkg))
                        {
                            packageBuilder.Save(fs);
                            _buildOptions.Reports.Quiet.WriteLine("{0} -> {1}", _currentProject.Name, Path.GetFullPath(nupkg));
                        }

                        if (symbolPackageBuilder.Files.Any())
                        {
                            using (var fs = File.Create(symbolsNupkg))
                            {
                                symbolPackageBuilder.Save(fs);
                                _buildOptions.Reports.Quiet.WriteLine("{0} -> {1}", _currentProject.Name, Path.GetFullPath(symbolsNupkg));
                            }
                        }
                    }
                }
            }

            if (!success)
            {
                return false;
            }

            if (!ScriptExecutor.Execute(_currentProject, "postbuild", GetScriptVariable))
            {
                LogError(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (_buildOptions.GeneratePackages &&
                !ScriptExecutor.Execute(_currentProject, "postpack", GetScriptVariable))
            {
                LogError(ScriptExecutor.ErrorMessage);
                return false;
            }

            sw.Stop();

            if (projectDiagnostics.Any())
            {
                // Add a new line to separate the project diagnostics information from compilation diagnostics
                _buildOptions.Reports.Information.WriteLine();

                projectDiagnostics.ForEach(d => LogWarning(d.FormattedMessage));
            }

            allDiagnostics.AddRange(projectDiagnostics);
            WriteSummary(allDiagnostics);

            _buildOptions.Reports.Information.WriteLine("Time elapsed {0}", sw.Elapsed);
            return success;
        }

        private string GetScriptVariable(string key)
        {
            if (string.Equals("project:BuildOutputDir", key, StringComparison.OrdinalIgnoreCase))
            {
                return GetBuildOutputDir(_currentProject);
            }

            return null;
        }

        private static void InitializeBuilder(Runtime.Project project, PackageBuilder builder)
        {
            builder.Authors.AddRange(project.Authors);
            builder.Owners.AddRange(project.Owners);

            if (builder.Authors.Count == 0)
            {
                // TODO: DNX_AUTHOR is a temporary name
                var defaultAuthor = Environment.GetEnvironmentVariable("DNX_AUTHOR");
                if (string.IsNullOrEmpty(defaultAuthor))
                {
                    builder.Authors.Add(project.Name);
                }
                else
                {
                    builder.Authors.Add(defaultAuthor);
                }
            }

            builder.Description = project.Description ?? project.Name;
            builder.Id = project.Name;
            builder.Version = project.Version;
            builder.Title = project.Title;
            builder.Summary = project.Summary;
            builder.Copyright = project.Copyright;
            builder.RequireLicenseAcceptance = project.RequireLicenseAcceptance;
            builder.ReleaseNotes = project.ReleaseNotes;
            builder.Language = project.Language;
            builder.Tags.AddRange(project.Tags);

            if (!string.IsNullOrEmpty(project.IconUrl))
            {
                builder.IconUrl = new Uri(project.IconUrl);
            }

            if (!string.IsNullOrEmpty(project.ProjectUrl))
            {
                builder.ProjectUrl = new Uri(project.ProjectUrl);
            }

            if (!string.IsNullOrEmpty(project.LicenseUrl))
            {
                builder.LicenseUrl = new Uri(project.LicenseUrl);
            }
        }

        private void WriteSummary(List<DiagnosticMessage> diagnostics)
        {
            _buildOptions.Reports.Information.WriteLine();

            var errorCount = diagnostics.Count(d => d.Severity == DiagnosticMessageSeverity.Error);
            var warningCount = diagnostics.Count(d => d.Severity == DiagnosticMessageSeverity.Warning);
            if (errorCount > 0)
            {
                LogError("Build failed.");
            }
            else
            {
                _buildOptions.Reports.Information.WriteLine("Build succeeded.".Green());
            }

            _buildOptions.Reports.Information.WriteLine("    {0} Warning(s)", warningCount);
            _buildOptions.Reports.Information.WriteLine("    {0} Error(s)", errorCount);

            _buildOptions.Reports.Information.WriteLine();
        }

        private void WriteDiagnostics(List<DiagnosticMessage> diagnostics)
        {
            var errors = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Error)
                                    .Select(d => d.FormattedMessage);
            foreach (var error in errors)
            {
                LogError(error);
            }

            var warnings = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Warning)
                                      .Select(d => d.FormattedMessage);

            foreach (var warning in warnings)
            {
                LogWarning(warning);
            }
        }

        private void LogError(string message)
        {
            _buildOptions.Reports.Error.WriteLine(message.Red().Bold());
        }

        private void LogWarning(string message)
        {
            _buildOptions.Reports.Information.WriteLine(message.Yellow().Bold());
        }

        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }

        private static string GetPackagePath(Runtime.Project project, string outputPath, bool symbols = false)
        {
            string fileName = project.Name + "." + project.Version.GetNormalizedVersionString() + (symbols ? ".symbols" : "") + ".nupkg";
            return Path.Combine(outputPath, fileName);
        }

        private string GetBuildOutputDir(Runtime.Project project)
        {
            var projectPath = Normalize(project.ProjectDirectory);
            return _buildOptions.OutputDir ?? Path.Combine(projectPath, "bin");
        }
    }
}
