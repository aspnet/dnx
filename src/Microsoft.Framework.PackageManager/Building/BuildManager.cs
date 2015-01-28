// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class BuildManager
    {
        private readonly IServiceProvider _hostServices;
        private readonly IApplicationEnvironment _applicationEnvironment;
        private readonly BuildOptions _buildOptions;

        public BuildManager(IServiceProvider hostServices, BuildOptions buildOptions)
        {
            _hostServices = hostServices;
            _buildOptions = buildOptions;
            _buildOptions.ProjectDir = Normalize(buildOptions.ProjectDir);

            _applicationEnvironment = (IApplicationEnvironment)hostServices.GetService(typeof(IApplicationEnvironment));

            ScriptExecutor = new ScriptExecutor();
        }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public bool Build()
        {
            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(_buildOptions.ProjectDir, out project))
            {
                WriteError(string.Format("Unable to locate {0}.'", Runtime.Project.ProjectFileName));
                return false;
            }

            var sw = Stopwatch.StartNew();

            var baseOutputPath = GetBuildOutputDir(_buildOptions);
            var configurations = _buildOptions.Configurations.DefaultIfEmpty("Debug");

            var specifiedFrameworks = _buildOptions.TargetFrameworks
                .ToDictionary(f => f, Runtime.Project.ParseFrameworkName);

            var projectFrameworks = new HashSet<FrameworkName>(
                project.GetTargetFrameworks()
                       .Select(c => c.FrameworkName));

            IEnumerable<FrameworkName> frameworks = null;

            if (projectFrameworks.Count > 0)
            {
                // Specified target frameworks have to be a subset of
                // the project frameworks
                if (!ValidateFrameworks(projectFrameworks, specifiedFrameworks))
                {
                    return false;
                }

                frameworks = specifiedFrameworks.Count > 0 ? specifiedFrameworks.Values : (IEnumerable<FrameworkName>)projectFrameworks;
            }
            else
            {
                frameworks = new[] { _applicationEnvironment.RuntimeFramework };
            }

            if (_buildOptions.GeneratePackages &&
                !ScriptExecutor.Execute(project, "prepack", GetScriptVariable))
            {
                WriteError(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (!ScriptExecutor.Execute(project, "prebuild", GetScriptVariable))
            {
                WriteError(ScriptExecutor.ErrorMessage);
                return false;
            }

            var success = true;

            var allErrors = new List<string>();
            var allWarnings = new List<string>();

            var cacheContextAccessor = new CacheContextAccessor();
            var cache = new Cache(cacheContextAccessor);

            PackageBuilder packageBuilder = null;
            PackageBuilder symbolPackageBuilder = null;
            InstallBuilder installBuilder = null;

            // Build all specified configurations
            foreach (var configuration in configurations)
            {
                if (_buildOptions.GeneratePackages)
                {
                    // Create a new builder per configuration
                    packageBuilder = new PackageBuilder();
                    symbolPackageBuilder = new PackageBuilder();
                    InitializeBuilder(project, packageBuilder);
                    InitializeBuilder(project, symbolPackageBuilder);

                    installBuilder = new InstallBuilder(project, packageBuilder, _buildOptions.Reports);
                }

                var configurationSuccess = true;

                baseOutputPath = Path.Combine(baseOutputPath, configuration);

                // Build all target frameworks a project supports
                foreach (var targetFramework in frameworks)
                {
                    _buildOptions.Reports.Information.WriteLine();
                    _buildOptions.Reports.Information.WriteLine("Building {0} for {1}",
                        project.Name, targetFramework.ToString().Yellow().Bold());

                    var errors = new List<string>();
                    var warnings = new List<string>();

                    var context = new BuildContext(_hostServices,
                                                   _applicationEnvironment,
                                                   cache,
                                                   cacheContextAccessor,
                                                   project,
                                                   targetFramework,
                                                   configuration,
                                                   baseOutputPath);

                    context.Initialize(_buildOptions.Reports.Quiet);

                    if (context.Build(warnings, errors))
                    {
                        if (_buildOptions.GeneratePackages)
                        {
                            context.PopulateDependencies(packageBuilder);
                            context.AddLibs(packageBuilder, "*.dll");
                            context.AddLibs(packageBuilder, "*.xml");
                            context.AddLibs(symbolPackageBuilder, "*.*");
                        }
                    }
                    else
                    {
                        configurationSuccess = false;
                    }

                    allErrors.AddRange(errors);
                    allWarnings.AddRange(warnings);

                    WriteDiagnostics(warnings, errors);
                }

                success = success && configurationSuccess;

                if (_buildOptions.GeneratePackages)
                {
                    // Create a package per configuration
                    string nupkg = GetPackagePath(project, baseOutputPath);
                    string symbolsNupkg = GetPackagePath(project, baseOutputPath, symbols: true);

                    if (configurationSuccess)
                    {
                        // Generates the application package only if this is an application packages
                        configurationSuccess = installBuilder.Build(baseOutputPath);
                        success = success && configurationSuccess;
                    }

                    if (configurationSuccess)
                    {
                        foreach (var sharedFile in project.SharedFiles)
                        {
                            var file = new PhysicalPackageFile();
                            file.SourcePath = sharedFile;
                            file.TargetPath = String.Format(@"shared\{0}", Path.GetFileName(sharedFile));
                            packageBuilder.Files.Add(file);
                        }

                        var root = project.ProjectDirectory;

                        foreach (var path in project.SourceFiles)
                        {
                            var srcFile = new PhysicalPackageFile();
                            srcFile.SourcePath = path;
                            srcFile.TargetPath = Path.Combine("src", PathUtility.GetRelativePath(root, path));
                            symbolPackageBuilder.Files.Add(srcFile);
                        }

                        using (var fs = File.Create(nupkg))
                        {
                            packageBuilder.Save(fs);
                            _buildOptions.Reports.Quiet.WriteLine("{0} -> {1}", project.Name, nupkg);
                        }

                        if (symbolPackageBuilder.Files.Any())
                        {
                            using (var fs = File.Create(symbolsNupkg))
                            {
                                symbolPackageBuilder.Save(fs);
                            }

                            _buildOptions.Reports.Quiet.WriteLine("{0} -> {1}", project.Name, symbolsNupkg);
                        }
                    }
                }
            }

            if (!ScriptExecutor.Execute(project, "postbuild", GetScriptVariable))
            {
                WriteError(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (_buildOptions.GeneratePackages &&
                !ScriptExecutor.Execute(project, "postpack", GetScriptVariable))
            {
                WriteError(ScriptExecutor.ErrorMessage);
                return false;
            }

            sw.Stop();

            WriteSummary(allWarnings, allErrors);

            _buildOptions.Reports.Information.WriteLine("Time elapsed {0}", sw.Elapsed);
            return success;
        }

        

        private bool ValidateFrameworks(HashSet<FrameworkName> projectFrameworks, IDictionary<string, FrameworkName> specifiedFrameworks)
        {
            bool success = true;

            foreach (var framework in specifiedFrameworks)
            {
                if (!projectFrameworks.Contains(framework.Value))
                {
                    WriteError(framework.Key + " is not specified in project.json");
                    success = false;
                }
            }

            return success;
        }

        private string GetScriptVariable(string key)
        {
            if (string.Equals("project:BuildOutputDir", key, StringComparison.OrdinalIgnoreCase))
            {
                return GetBuildOutputDir(_buildOptions);
            }

            return null;
        }

        private static void InitializeBuilder(Runtime.Project project, PackageBuilder builder)
        {
            builder.Authors.AddRange(project.Authors);

            if (builder.Authors.Count == 0)
            {
                // TODO: K_AUTHOR is a temporary name
                var defaultAuthor = Environment.GetEnvironmentVariable("K_AUTHOR");
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
            builder.Title = project.Name;
            builder.RequireLicenseAcceptance = project.RequireLicenseAcceptance;
            builder.Tags.AddRange(project.Tags);

            if (!string.IsNullOrEmpty(project.ProjectUrl))
            {
                builder.ProjectUrl = new Uri(project.ProjectUrl);
            }
        }

        private void WriteSummary(List<string> warnings, List<string> errors)
        {
            _buildOptions.Reports.Information.WriteLine();

            if (errors.Count > 0)
            {
                WriteError("Build failed.");
            }
            else
            {
                _buildOptions.Reports.Information.WriteLine("Build succeeded.".Green());
            }

            _buildOptions.Reports.Information.WriteLine("    {0} Warnings(s)", warnings.Count);
            _buildOptions.Reports.Information.WriteLine("    {0} Error(s)", errors.Count);

            _buildOptions.Reports.Information.WriteLine();
        }

        private void WriteDiagnostics(List<string> warnings, List<string> errors)
        {
            foreach (var error in errors)
            {
                WriteError(error);
            }

            foreach (var warning in warnings)
            {
                WriteWarning(warning);
            }
        }

        private void WriteError(string message)
        {
            _buildOptions.Reports.Error.WriteLine(message.Red());
        }

        private void WriteWarning(string message)
        {
            _buildOptions.Reports.Information.WriteLine(message.Yellow());
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
            string fileName = project.Name + "." + project.Version + (symbols ? ".symbols" : "") + ".nupkg";
            return Path.Combine(outputPath, fileName);
        }

        private static string GetBuildOutputDir(BuildOptions buildOptions)
        {
            return buildOptions.OutputDir ?? Path.Combine(buildOptions.ProjectDir, "bin");
        }
    }
}
