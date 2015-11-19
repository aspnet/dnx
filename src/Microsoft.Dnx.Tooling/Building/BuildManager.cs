// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Runtime.Internals;
using Microsoft.Dnx.Tooling.Utils;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public class BuildManager
    {
        private readonly IApplicationEnvironment _applicationEnvironment;
        private readonly BuildOptions _buildOptions;

        // Shared by all projects that will be built by this class
        private CompilationEngine _compilationEngine;

        private Runtime.Project _currentProject;

        public BuildManager(BuildOptions buildOptions)
        {
            _buildOptions = buildOptions;

            _applicationEnvironment = PlatformServices.Default.Application;
            var runtimeEnvironment = PlatformServices.Default.Runtime;
            var loadContextAccessor = PlatformServices.Default.AssemblyLoadContextAccessor;

            _compilationEngine = new CompilationEngine(new CompilationEngineContext(
                _applicationEnvironment,
                runtimeEnvironment,
                loadContextAccessor.Default,
                new CompilationCache()));

            ScriptExecutor = new ScriptExecutor(buildOptions.Reports.Information);
        }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public bool Build()
        {
            var projectFilesFinder = new Matcher();

            // Resolve all the project names
            var projectFilesToBuild = new List<string>();
            foreach (var pattern in _buildOptions.ProjectPatterns)
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
                Path.Combine(rootDirectory, file.Path));
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

            var success = true;

            var allDiagnostics = new List<DiagnosticMessage>();

            // Build all specified configurations
            foreach (var configuration in configurations)
            {
                success &= BuildConfiguration(baseOutputPath, frameworks, allDiagnostics, configuration);
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

        private bool BuildConfiguration(string baseOutputPath, IEnumerable<FrameworkName> frameworks, List<DiagnosticMessage> allDiagnostics, string configuration)
        {
            PackageBuilder packageBuilder = null;
            PackageBuilder symbolPackageBuilder = null;
            InstallBuilder installBuilder = null;

            var contextVariables = new Dictionary<string, string>
            {
                { "project:BuildOutputDir", GetBuildOutputDir(_currentProject) },
                { "build:Configuration", configuration }
            };

            var getScriptVariable = GetScriptVariable(contextVariables);

            if (_buildOptions.GeneratePackages)
            {
                if(!ScriptExecutor.Execute(_currentProject, "prepack", getScriptVariable))
                {
                    LogError(ScriptExecutor.ErrorMessage);
                    return false;
                }

                // Create a new builder per configuration
                packageBuilder = new PackageBuilder();
                symbolPackageBuilder = new PackageBuilder();
                InitializeBuilder(_currentProject, packageBuilder);
                InitializeBuilder(_currentProject, symbolPackageBuilder);
                installBuilder = new InstallBuilder(_currentProject, packageBuilder, _buildOptions.Reports);
            }

            var success = true;

            var outputPath = Path.Combine(baseOutputPath, configuration);

            // Build all target frameworks a project supports
            foreach (var targetFramework in frameworks)
            {
                _buildOptions.Reports.Information.WriteLine();
                _buildOptions.Reports.Information.WriteLine("Building {0} for {1}",
                    _currentProject.Name, targetFramework.ToString().Yellow().Bold());

                contextVariables["build:TargetFramework"] = VersionUtility.GetShortFrameworkName(targetFramework);

                if (!ScriptExecutor.Execute(_currentProject, "prebuild", getScriptVariable))
                {
                    LogError(ScriptExecutor.ErrorMessage);
                    success = false;
                    continue;
                }

                var diagnostics = new List<DiagnosticMessage>();
                var context = new BuildContext(_compilationEngine,
                                               _currentProject,
                                               targetFramework,
                                               configuration,
                                               outputPath);

                context.Initialize(_buildOptions.Reports.Quiet);

                success &= context.Build(diagnostics);

                if (success)
                {
                    if (_buildOptions.GeneratePackages)
                    {
                        context.PopulateDependencies(packageBuilder);
                        context.AddLibs(packageBuilder, "*.dll");
                        context.AddLibs(packageBuilder, "*.xml");

                        context.PopulateDependencies(symbolPackageBuilder);
                        context.AddLibs(symbolPackageBuilder, "*.*");

                        context.AddLibs(packageBuilder, "*.resources.dll", recursiveSearch: true);
                    }

                    if (!ScriptExecutor.Execute(_currentProject, "postbuild", getScriptVariable))
                    {
                        LogError(ScriptExecutor.ErrorMessage);
                        success = false;
                    }
                }

                allDiagnostics.AddRange(diagnostics);

                WriteDiagnostics(diagnostics);

                contextVariables.Remove("build:TargetFramework");
            }

            if (_buildOptions.GeneratePackages)
            {
                success = success &&
                    // Generates the application package only if this is an application packages
                    installBuilder.Build(outputPath);

                if (success)
                {
                    // Create a package per configuration
                    var nupkg = GetPackagePath(_currentProject, outputPath);
                    var symbolsNupkg = GetPackagePath(_currentProject, outputPath, symbols: true);

                    success &= GeneratePackage(success, allDiagnostics, packageBuilder, symbolPackageBuilder, nupkg, symbolsNupkg);
                }

                if (success)
                {
                    if (!ScriptExecutor.Execute(_currentProject, "postpack", getScriptVariable))
                    {
                        LogError(ScriptExecutor.ErrorMessage);
                        return false;
                    }
                }
            }

            return success;
        }

        private bool GeneratePackage(bool success, List<DiagnosticMessage> allDiagnostics, PackageBuilder packageBuilder, PackageBuilder symbolPackageBuilder, string nupkg, string symbolsNupkg)
        {
            var packDiagnostics = new List<DiagnosticMessage>();
            foreach (var sharedFile in _currentProject.Files.SharedFiles)
            {
                var file = new PhysicalPackageFile();
                file.SourcePath = sharedFile;
                file.TargetPath = String.Format(@"shared\{0}", Path.GetFileName(sharedFile));
                packageBuilder.Files.Add(file);
            }

            var root = _currentProject.ProjectDirectory;

            if (_currentProject.Files.PackInclude != null && _currentProject.Files.PackInclude.Any())
            {
                AddPackageFiles(_currentProject.ProjectDirectory, _currentProject.Files.PackInclude, packageBuilder, packDiagnostics);
            }
            success &= !packDiagnostics.HasErrors();
            allDiagnostics.AddRange(packDiagnostics);

            foreach (var path in _currentProject.Files.SourceFiles)
            {
                var srcFile = new PhysicalPackageFile();
                srcFile.SourcePath = path;
                srcFile.TargetPath = Path.Combine("src", PathUtility.GetRelativePath(root, path));
                symbolPackageBuilder.Files.Add(srcFile);
            }

            // Write the packages as long as we're still in a success state.
            if (success)
            {
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

            WriteDiagnostics(packDiagnostics);
            return success;
        }

        private void AddPackageFiles(string projectDirectory, IEnumerable<PackIncludeEntry> packageFiles, PackageBuilder packageBuilder, IList<DiagnosticMessage> diagnostics)
        {
            var rootDirectory = new DirectoryInfoWrapper(new DirectoryInfo(projectDirectory));

            foreach (var match in CollectAdditionalFiles(rootDirectory, packageFiles, _currentProject.ProjectFilePath, diagnostics))
            {
                packageBuilder.Files.Add(match);
            }
        }

        internal static IEnumerable<PhysicalPackageFile> CollectAdditionalFiles(DirectoryInfoBase rootDirectory, IEnumerable<PackIncludeEntry> projectFileGlobs, string projectFilePath, IList<DiagnosticMessage> diagnostics)
        {
            foreach (var entry in projectFileGlobs)
            {
                // Evaluate the globs on the right
                var matcher = new Matcher();
                matcher.AddIncludePatterns(entry.SourceGlobs);
                var results = matcher.Execute(rootDirectory);
                var files = results.Files.ToList();

                // Check for illegal characters
                if (string.IsNullOrEmpty(entry.Target))
                {
                    diagnostics.Add(new DiagnosticMessage(
                        DiagnosticMonikers.NU1003,
                        $"Invalid '{ProjectFilesCollection.PackIncludePropertyName}' section. The target '{entry.Target}' is invalid, " +
                        "targets must either be a file name or a directory suffixed with '/'. " +
                        "The root directory of the package can be specified by using a single '/' character.",
                        projectFilePath,
                        DiagnosticMessageSeverity.Error,
                        entry.Line,
                        entry.Column));
                    continue;
                }

                if (entry.Target.Split('/').Any(s => s.Equals(".") || s.Equals("..")))
                {
                    diagnostics.Add(new DiagnosticMessage(
                        DiagnosticMonikers.NU1004,
                        $"Invalid '{ProjectFilesCollection.PackIncludePropertyName}' section. " +
                        $"The target '{entry.Target}' contains path-traversal characters ('.' or '..'). " +
                        "These characters are not permitted in target paths.",
                        projectFilePath,
                        DiagnosticMessageSeverity.Error,
                        entry.Line,
                        entry.Column));
                    continue;
                }

                // Check the arity of the left
                if (entry.Target.EndsWith("/"))
                {
                    var dir = entry.Target.Substring(0, entry.Target.Length - 1).Replace('/', Path.DirectorySeparatorChar);

                    foreach (var file in files)
                    {
                        yield return new PhysicalPackageFile()
                        {
                            SourcePath = Path.Combine(rootDirectory.FullName, PathUtility.GetPathWithDirectorySeparator(file.Path)),
                            TargetPath = Path.Combine(dir, PathUtility.GetPathWithDirectorySeparator(file.Stem))
                        };
                    }
                }
                else
                {
                    // It's a file. If the glob matched multiple things, we're sad :(
                    if (files.Count > 1)
                    {
                        // Arity mismatch!
                        string sourceValue = entry.SourceGlobs.Length == 1 ?
                            $"\"{entry.SourceGlobs[0]}\"" :
                            ("[" + string.Join(",", entry.SourceGlobs.Select(v => $"\"{v}\"")) + "]");
                        diagnostics.Add(new DiagnosticMessage(
                            DiagnosticMonikers.NU1005,
                            $"Invalid '{ProjectFilesCollection.PackIncludePropertyName}' section. " +
                            $"The target '{entry.Target}' refers to a single file, but the pattern {sourceValue} " +
                            "produces multiple files. To mark the target as a directory, suffix it with '/'.",
                            projectFilePath,
                            DiagnosticMessageSeverity.Error,
                            entry.Line,
                            entry.Column));
                    }
                    else
                    {
                        yield return new PhysicalPackageFile()
                        {
                            SourcePath = Path.Combine(rootDirectory.FullName, files[0].Path),
                            TargetPath = PathUtility.GetPathWithDirectorySeparator(entry.Target)
                        };
                    }
                }
            }
        }

        private Func<string, string> GetScriptVariable(IDictionary<string, string> contextVariables)
        {
            return key =>
            {
                string value;
                contextVariables.TryGetValue(key, out value);
                return value;
            };
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
            string fileName = $"{project.Name}.{project.Version}{(symbols ? ".symbols" : string.Empty)}{NuGet.Constants.PackageExtension}";
            return Path.Combine(outputPath, fileName);
        }

        private string GetBuildOutputDir(Runtime.Project project)
        {
            var projectPath = Normalize(project.ProjectDirectory);
            return _buildOptions.OutputDir ?? Path.Combine(projectPath, "bin");
        }
    }
}
