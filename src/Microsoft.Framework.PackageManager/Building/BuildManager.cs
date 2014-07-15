// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Roslyn;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class BuildManager
    {
        private readonly BuildOptions _buildOptions;

        public BuildManager(BuildOptions buildOptions)
        {
            _buildOptions = buildOptions;
            _buildOptions.ProjectDir = Normalize(buildOptions.ProjectDir);
        }

        public bool Build()
        {
            Project project;
            if (!Project.TryGetProject(_buildOptions.ProjectDir, out project))
            {
                Console.WriteLine("Unable to locate {0}.'", Project.ProjectFileName);
                return false;
            }

            var sw = Stopwatch.StartNew();

            var outputPath = _buildOptions.OutputDir ?? Path.Combine(_buildOptions.ProjectDir, "bin");
            var configurations = _buildOptions.Configurations.DefaultIfEmpty("debug");

            var specifiedFrameworks = _buildOptions.TargetFrameworks
                .ToDictionary(f => f, Project.ParseFrameworkName);

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

            var success = true;

            var allDiagnostics = new List<Diagnostic>();

            // Build all specified configurations
            foreach (var configuration in configurations)
            {
                // Create a new builder per configuration
                var builder = new PackageBuilder();
                var symbolPackageBuilder = new PackageBuilder();

                InitializeBuilder(project, builder);
                InitializeBuilder(project, symbolPackageBuilder);

                var configurationSuccess = true;

                outputPath = Path.Combine(outputPath, configuration);

                // Build all target frameworks a project supports
                foreach (var targetFramework in frameworks)
                {
                    try
                    {
                        var diagnostics = new List<Diagnostic>();

                        if (_buildOptions.CheckDiagnostics)
                        {
                            configurationSuccess = configurationSuccess &&
                                                    CheckDiagnostics(project,
                                                                     targetFramework,
                                                                     configuration,
                                                                     diagnostics);
                        }
                        else
                        {
                            configurationSuccess = configurationSuccess && 
                                                   Build(project,
                                                         outputPath,
                                                         targetFramework,
                                                         configuration,
                                                         builder,
                                                         symbolPackageBuilder,
                                                         diagnostics);
                        }

                        allDiagnostics.AddRange(diagnostics);

                        WriteDiagnostics(diagnostics);
                    }
                    catch (Exception ex)
                    {
                        configurationSuccess = false;
                        WriteError(ex.ToString());
                    }
                }

                success = success && configurationSuccess;

                if (_buildOptions.CheckDiagnostics)
                {
                    continue;
                }

                // Create a package per configuration
                string nupkg = GetPackagePath(project, outputPath);
                string symbolsNupkg = GetPackagePath(project, outputPath, symbols: true);

                // If there were any errors then don't create the package
                if (!allDiagnostics.Any(IsError) && configurationSuccess)
                {
                    foreach (var sharedFile in project.SharedFiles)
                    {
                        var file = new PhysicalPackageFile();
                        file.SourcePath = sharedFile;
                        file.TargetPath = String.Format(@"shared\{0}", Path.GetFileName(sharedFile));
                        builder.Files.Add(file);
                    }

                    using (var fs = File.Create(nupkg))
                    {
                        builder.Save(fs);
                    }

                    using (var fs = File.Create(symbolsNupkg))
                    {
                        symbolPackageBuilder.Save(fs);
                    }

                    Console.WriteLine("{0} -> {1}", project.Name, nupkg);
                    Console.WriteLine("{0} -> {1}", project.Name, symbolsNupkg);
                }
            }

            sw.Stop();

            WriteSummary(allDiagnostics);

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
                    Console.WriteLine(framework.Key + " is not specified in project.json");
                    success = false;
                }
            }

            return success;
        }

        private static void InitializeBuilder(Project project, PackageBuilder builder)
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

        private void WriteSummary(List<Diagnostic> allDiagnostics)
        {
            var errors = allDiagnostics.Count(IsError);
            var warnings = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

            Console.WriteLine();

            if (errors > 0)
            {
                WriteColor("Build failed.", ConsoleColor.Red);
            }
            else
            {
                WriteColor("Build succeeded.", ConsoleColor.Green);
            }

            Console.WriteLine("    {0} Warnings(s)", warnings);
            Console.WriteLine("    {0} Error(s)", errors);

            Console.WriteLine();
        }

        private void WriteDiagnostics(List<Diagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
            {
                string message = GetMessage(diagnostic);

                if (IsError(diagnostic))
                {
                    WriteError(message);
                }
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    WriteWarning(message);
                }
            }
        }

        private void WriteError(string message)
        {
            WriteColor(message, ConsoleColor.Red);
        }

        private void WriteWarning(string message)
        {
            WriteColor(message, ConsoleColor.Yellow);
        }

        private void WriteColor(string message, ConsoleColor color)
        {
            var old = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = old;
            }
        }

        private bool CheckDiagnostics(Project project,
                                      FrameworkName targetFramework,
                                      string configuration,
                                      List<Diagnostic> diagnostics)
        {
            var compiler = PrepareCompiler(project, targetFramework);
            var compilationContext = compiler.CompileProject(project.Name, targetFramework, configuration);

            if (compilationContext == null)
            {
                return false;
            }

            diagnostics.AddRange(compilationContext.Diagnostics);
            diagnostics.AddRange(compilationContext.Compilation.GetDiagnostics());

            return true;
        }

        private bool Build(Project project,
                           string outputPath,
                           FrameworkName targetFramework,
                           string configuration,
                           PackageBuilder builder,
                           PackageBuilder symbolPackageBuilder,
                           List<Diagnostic> diagnostics)
        {
            IDictionary<string, string> packagePaths;
            var roslynArtifactsProducer = PrepareArtifactsProducer(project, targetFramework, out packagePaths);

            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(targetFramework);
            string targetPath = Path.Combine(outputPath, targetFrameworkFolder);

            var buildContext = new BuildContext(project.Name, targetFramework, configuration)
            {
                OutputPath = targetPath,
                PackageBuilder = builder,
                SymbolPackageBuilder = symbolPackageBuilder
            };

            Console.WriteLine("Building {0} {1}", project.Name, targetFramework);

            if (!roslynArtifactsProducer.Build(buildContext, diagnostics))
            {
                return false;
            }

            return true;
        }

        private static RoslynArtifactsProducer PrepareArtifactsProducer(Project project, FrameworkName targetFramework, out IDictionary<string, string> packagePaths)
        {
            var projectDir = project.ProjectDirectory;
            var rootDirectory = ProjectResolver.ResolveRootDirectory(projectDir);
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);
            var packagesDir = NuGetDependencyResolver.ResolveRepositoryPath(rootDirectory);

            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();
            var compositeResourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
            var nugetDependencyResolver = new NuGetDependencyResolver(packagesDir, referenceAssemblyDependencyResolver.FrameworkResolver);
            var gacDependencyResolver = new GacDependencyResolver();
            var compositeDependencyExporter = new CompositeLibraryExportProvider(new ILibraryExportProvider[] {
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver,
            });

            var roslynCompiler = new RoslynCompiler(projectResolver,
                                                    NoopWatcher.Instance,
                                                    compositeDependencyExporter);

            var projectReferenceResolver = new ProjectReferenceDependencyProvider(projectResolver);

            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                projectReferenceResolver,
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver
            });

            dependencyWalker.Walk(project.Name, project.Version, targetFramework);

            var roslynArtifactsProducer = new RoslynArtifactsProducer(roslynCompiler,
                                                                      compositeResourceProvider,
                                                                      projectReferenceResolver.Dependencies,
                                                                      referenceAssemblyDependencyResolver.FrameworkResolver);

            packagePaths = nugetDependencyResolver.PackageAssemblyPaths;

            return roslynArtifactsProducer;
        }

        private IRoslynCompiler PrepareCompiler(Project project, FrameworkName targetFramework)
        {
            var projectDir = project.ProjectDirectory;
            var rootDirectory = ProjectResolver.ResolveRootDirectory(projectDir);
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);
            var packagesDir = NuGetDependencyResolver.ResolveRepositoryPath(rootDirectory);

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
            var nugetDependencyResolver = new NuGetDependencyResolver(packagesDir, referenceAssemblyDependencyResolver.FrameworkResolver);
            var gacDependencyResolver = new GacDependencyResolver();
            var compositeDependencyExporter = new CompositeLibraryExportProvider(new ILibraryExportProvider[] {
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver,
            });

            var roslynCompiler = new RoslynCompiler(projectResolver,
                                                    NoopWatcher.Instance,
                                                    compositeDependencyExporter);

            var projectReferenceResolver = new ProjectReferenceDependencyProvider(projectResolver);

            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                projectReferenceResolver,
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver
            });

            dependencyWalker.Walk(project.Name, project.Version, targetFramework);


            return roslynCompiler;
        }

        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }

        private static string GetPackagePath(Project project, string outputPath, bool symbols = false)
        {
            string fileName = project.Name + "." + project.Version + (symbols ? ".symbols" : "") + ".nupkg";
            return Path.Combine(outputPath, fileName);
        }

        private static string GetMessage(Diagnostic diagnostic)
        {
            var formatter = new DiagnosticFormatter();

            return formatter.Format(diagnostic);
        }

        private static bool IsError(Diagnostic diagnostic)
        {
            return diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error;
        }
    }
}
