// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class BuildManager
    {
        private readonly IServiceProvider _hostServices;
        private readonly IAssemblyLoaderContainer _loaderContainer;
        private readonly IApplicationEnvironment _applicationEnvironment;
        private readonly BuildOptions _buildOptions;
        private string _configuration;
        private string _targetFramework;

        public BuildManager(IServiceProvider hostServices, BuildOptions buildOptions)
        {
            _hostServices = hostServices;
            _buildOptions = buildOptions;
            _buildOptions.ProjectDir = Normalize(buildOptions.ProjectDir);

            _applicationEnvironment = (IApplicationEnvironment)hostServices.GetService(typeof(IApplicationEnvironment));
            _loaderContainer = (IAssemblyLoaderContainer)hostServices.GetService(typeof(IAssemblyLoaderContainer));

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

            ScriptExecutor.Execute(project, _buildOptions.Reports, "prebuild", GetScriptVariable);

            var success = true;

            var allErrors = new List<string>();
            var allWarnings = new List<string>();

            // Initialize the default host so that we can load custom project export
            // providers from nuget packages/projects
            var host = new DefaultHost(new DefaultHostOptions()
            {
                ApplicationBaseDirectory = project.ProjectDirectory,
                TargetFramework = _applicationEnvironment.RuntimeFramework,
                Configuration = _applicationEnvironment.Configuration
            },
            _hostServices);

            host.Initialize();

            var cacheContextAccessor = new CacheContextAccessor();
            var cache = new Cache(cacheContextAccessor);

            using (host.AddLoaders(_loaderContainer))
            {
                // Build all specified configurations
                foreach (var configuration in configurations)
                {
                    _configuration = configuration;
                    ScriptExecutor.Execute(project, _buildOptions.Reports, "prebuild.perconfiguration", GetScriptVariable);

                    // Create a new builder per configuration
                    var packageBuilder = new PackageBuilder();
                    var symbolPackageBuilder = new PackageBuilder();

                    InitializeBuilder(project, packageBuilder);
                    InitializeBuilder(project, symbolPackageBuilder);

                    var configurationSuccess = true;

                    baseOutputPath = Path.Combine(baseOutputPath, configuration);

                    // Build all target frameworks a project supports
                    foreach (var targetFramework in frameworks)
                    {
                        _targetFramework = targetFramework.ToString();

                        _buildOptions.Reports.Information.WriteLine();
                        _buildOptions.Reports.Information.WriteLine("Building {0} for {1}",
                            project.Name, _targetFramework.Yellow().Bold());

                        ScriptExecutor.Execute(project, _buildOptions.Reports, "prebuild.perframework", GetScriptVariable);

                        var errors = new List<string>();
                        var warnings = new List<string>();

                        var context = new BuildContext(cache,
                                                       cacheContextAccessor,
                                                       project,
                                                       targetFramework,
                                                       configuration,
                                                       baseOutputPath);
                        context.Initialize(_buildOptions.Reports.Quiet);

                        if (context.Build(warnings, errors))
                        {
                            context.PopulateDependencies(packageBuilder);
                            context.AddLibs(packageBuilder, "*.dll");
                            context.AddLibs(packageBuilder, "*.xml");
                            context.AddLibs(symbolPackageBuilder, "*.*");
                        }
                        else
                        {
                            configurationSuccess = false;
                        }

                        allErrors.AddRange(errors);
                        allWarnings.AddRange(warnings);

                        WriteDiagnostics(warnings, errors);

                        _targetFramework = null;
                    }

                    success = success && configurationSuccess;

                    // Create a package per configuration
                    string nupkg = GetPackagePath(project, baseOutputPath);
                    string symbolsNupkg = GetPackagePath(project, baseOutputPath, symbols: true);

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

            _configuration = null;
            ScriptExecutor.Execute(project, _buildOptions.Reports, "postbuild", GetScriptVariable);

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
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "project:BuildOutputDir", GetBuildOutputDir(_buildOptions) },
                { "project:Configuration", _configuration },
                { "project:TargetFramework", _targetFramework },
            };

            string variable;
            variables.TryGetValue(key, out variable);

            return variable;
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
