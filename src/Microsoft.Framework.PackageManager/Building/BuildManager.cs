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
        private readonly BuildOptions _buildOptions;

        public BuildManager(IServiceProvider hostServices, BuildOptions buildOptions)
        {
            _hostServices = hostServices;
            _buildOptions = buildOptions;
            _buildOptions.ProjectDir = Normalize(buildOptions.ProjectDir);

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

            var baseOutputPath = _buildOptions.OutputDir ?? Path.Combine(_buildOptions.ProjectDir, "bin");
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
                frameworks = new[] { _buildOptions.RuntimeTargetFramework };
            }

            Func<string, string> getVariable = key =>
            {
                return null;
            };

            ScriptExecutor.Execute(project, "prebuild", getVariable);

            var success = true;

            var allErrors = new List<string>();
            var allWarnings = new List<string>();

            var applicationEnvironment = (IApplicationEnvironment)_hostServices.GetService(typeof(IApplicationEnvironment));
            var loaderContainer = (IAssemblyLoaderContainer)_hostServices.GetService(typeof(IAssemblyLoaderContainer));

            // Initialize the default host so that we can load custom project export
            // providers from nuget packages/projects
            var host = new DefaultHost(new DefaultHostOptions()
            {
                ApplicationBaseDirectory = project.ProjectDirectory,
                TargetFramework = applicationEnvironment.TargetFramework,
                Configuration = applicationEnvironment.Configuration
            },
            _hostServices);

            host.Initialize();

            var cacheContextAccessor = new CacheContextAccessor();
            var cache = new Cache(cacheContextAccessor);

            using (host.AddLoaders(loaderContainer))
            {
                // Build all specified configurations
                foreach (var configuration in configurations)
                {
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
                        var errors = new List<string>();
                        var warnings = new List<string>();

                        var context = new BuildContext(cache,
                                                       cacheContextAccessor,
                                                       project,
                                                       targetFramework,
                                                       configuration,
                                                       baseOutputPath);
                        context.Initialize();

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
                            Console.WriteLine("{0} -> {1}", project.Name, nupkg);
                        }

                        if (symbolPackageBuilder.Files.Any())
                        {
                            using (var fs = File.Create(symbolsNupkg))
                            {
                                symbolPackageBuilder.Save(fs);
                            }

                            Console.WriteLine("{0} -> {1}", project.Name, symbolsNupkg);
                        }
                    }
                }
            }

            ScriptExecutor.Execute(project, "postbuild", getVariable);

            sw.Stop();

            WriteSummary(allWarnings, allErrors);

            Console.WriteLine("Time elapsed {0}", sw.Elapsed);
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
        }

        private void WriteSummary(List<string> warnings, List<string> errors)
        {
            Console.WriteLine();

            if (errors.Count > 0)
            {
                WriteError("Build failed.");
            }
            else
            {
                WriteColor("Build succeeded.", ConsoleColor.Green);
            }

            Console.WriteLine("    {0} Warnings(s)", warnings.Count);
            Console.WriteLine("    {0} Error(s)", errors.Count);

            Console.WriteLine();
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
            WriteColor(Console.Error, message, ConsoleColor.Red);
        }

        private void WriteWarning(string message)
        {
            WriteColor(message, ConsoleColor.Yellow);
        }

        private void WriteColor(string message, ConsoleColor color)
        {
            WriteColor(Console.Out, message, color);
        }

        private void WriteColor(TextWriter writer, string message, ConsoleColor color)
        {
            var old = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = color;
                writer.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = old;
            }
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
    }
}
