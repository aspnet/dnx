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
using Microsoft.Framework.Runtime.Loader;
using Microsoft.Framework.Runtime.Loader.NuGet;
using Microsoft.Framework.Runtime.Roslyn;
using NuGet;
using KProject = Microsoft.Framework.Runtime.Project;

namespace Microsoft.Framework.Project
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
            KProject project;
            if (!KProject.TryGetProject(_buildOptions.ProjectDir, out project))
            {
                Console.WriteLine("Unable to locate {0}.'", KProject.ProjectFileName);
                return false;
            }

            var sw = Stopwatch.StartNew();

            string outputPath = _buildOptions.OutputDir ?? Path.Combine(_buildOptions.ProjectDir, "bin");
            string nupkg = GetPackagePath(project, outputPath);
            string symbolsNupkg = GetPackagePath(project, outputPath, symbols: true);

            var configurations = new HashSet<FrameworkName>(
                project.GetTargetFrameworkConfigurations()
                       .Select(c => c.FrameworkName));

            if (configurations.Count == 0)
            {
                configurations.Add(_buildOptions.RuntimeTargetFramework);
            }

            var builder = new PackageBuilder();
            var symbolPackageBuilder = new PackageBuilder();

            InitializeBuilder(project, builder);
            InitializeBuilder(project, symbolPackageBuilder);

            bool success = true;

            var allDiagnostics = new List<Diagnostic>();

            // Build all target frameworks a project supports
            foreach (var targetFramework in configurations)
            {
                try
                {
                    var diagnostics = new List<Diagnostic>();

                    success = success && Build(project,
                                               outputPath,
                                               targetFramework,
                                               builder,
                                               symbolPackageBuilder,
                                               diagnostics);

                    allDiagnostics.AddRange(diagnostics);

                    WriteDiagnostics(diagnostics);
                }
                catch (Exception ex)
                {
                    success = false;
                    WriteError(ex.ToString());
                }
            }

            foreach (var sharedFile in project.SharedFiles)
            {
                var file = new PhysicalPackageFile();
                file.SourcePath = sharedFile;
                file.TargetPath = String.Format(@"shared\{0}", Path.GetFileName(sharedFile));
                builder.Files.Add(file);
            }

            // If there were any errors then don't create the package
            if (!allDiagnostics.Any(IsError) && success)
            {
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

            sw.Stop();

            WriteSummary(allDiagnostics);

            Console.WriteLine("Time elapsed {0}", sw.Elapsed);
            return success;
        }

        private static void InitializeBuilder(KProject project, PackageBuilder builder)
        {
            builder.Authors.AddRange(project.Authors);

            if (builder.Authors.Count == 0)
            {
                builder.Authors.Add("K");
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
#if NET45
                WriteColor("Build failed.", ConsoleColor.Red);
#else
                Console.WriteLine("Build failed.");
#endif
            }
            else
            {
#if NET45
                WriteColor("Build succeeded.", ConsoleColor.Green);
#else
                Console.WriteLine("Build succeeded.");
#endif
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
#if NET45
            WriteColor(message, ConsoleColor.Red);
#else
            Console.WriteLine(message);
#endif
        }

        private void WriteWarning(string message)
        {
#if NET45
            WriteColor(message, ConsoleColor.Yellow);
#else
            Console.WriteLine(message);
#endif
        }

#if NET45
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
#endif

        private bool Build(KProject project,
                           string outputPath,
                           FrameworkName targetFramework,
                           PackageBuilder builder,
                           PackageBuilder symbolPackageBuilder,
                           List<Diagnostic> diagnostics)
        {
            IDictionary<string, string> packagePaths;
            var roslynArtifactsProducer = PrepareCompiler(project, targetFramework, out packagePaths);

            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(targetFramework);
            string targetPath = Path.Combine(outputPath, targetFrameworkFolder);

            var buildContext = new BuildContext(project.Name, targetFramework)
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

            if (_buildOptions.GenerateNativeImages &&
                !VersionUtility.IsDesktop(targetFramework) &&
                !VersionUtility.IsPortableFramework(targetFramework))
            {
                // Generate native images
                var options = new CrossgenOptions
                {
                    CrossgenPath = _buildOptions.CrossgenPath,
                    RuntimePath = _buildOptions.RuntimePath
                };

                // TODO: We want to generate native images for packages in a sibling folder.
                // This is temporary
                options.InputPaths = packagePaths.Values
                                        .Select(path => Path.GetDirectoryName(path))
                                        .Concat(new[] { targetPath });

                // TODO: Project references

                var crossgen = new CrossgenManager(options);
                return crossgen.GenerateNativeImages();
            }

            return true;
        }

        private static RoslynArtifactsProducer PrepareCompiler(KProject project, FrameworkName targetFramework, out IDictionary<string, string> packagePaths)
        {
            var projectDir = project.ProjectDirectory;
            var rootDirectory = DefaultHost.ResolveRootDirectory(projectDir);
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);

            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();
            var compositeResourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });

            var nugetDependencyResolver = new NuGetDependencyResolver(projectDir);
            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
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

        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }

        private static string GetPackagePath(KProject project, string outputPath, bool symbols = false)
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
