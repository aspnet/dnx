using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Loader.NuGet;
using Microsoft.Net.Runtime.Roslyn;
using NuGet;
using KProject = Microsoft.Net.Runtime.Project;

namespace Microsoft.Net.Project
{
    public class ProjectManager
    {
        private readonly BuildOptions _buildOptions;

        public ProjectManager(BuildOptions buildOptions)
        {
            _buildOptions = buildOptions;
            _buildOptions.ProjectDir = Normalize(buildOptions.ProjectDir);
        }

        public bool Build()
        {
            KProject project;
            if (!KProject.TryGetProject(_buildOptions.ProjectDir, out project))
            {
                System.Console.WriteLine("Unable to locate {0}.'", KProject.ProjectFileName);
                return false;
            }

            var sw = Stopwatch.StartNew();

            string outputPath = _buildOptions.OutputDir ?? Path.Combine(_buildOptions.ProjectDir, "bin");
            string nupkg = GetPackagePath(project, outputPath);

            var configurations = new HashSet<FrameworkName>(
                project.GetTargetFrameworkConfigurations()
                       .Select(c => c.FrameworkName));

            if (configurations.Count == 0)
            {
                configurations.Add(VersionUtility.ParseFrameworkName(_buildOptions.RuntimeTargetFramework));
            }

            var builder = new PackageBuilder();

            // TODO: Support nuspecs in the project folder
            builder.Authors.AddRange(project.Authors);

            if (builder.Authors.Count == 0)
            {
                // Temporary
                builder.Authors.Add("K");
            }

            builder.Description = project.Description ?? project.Name;
            builder.Id = project.Name;
            builder.Version = project.Version;
            builder.Title = project.Name;

            bool success = true;
            bool createPackage = false;

            var allDiagnostics = new List<Diagnostic>();

            // Build all target frameworks a project supports
            foreach (var targetFramework in configurations)
            {
                try
                {
                    var diagnostics = new List<Diagnostic>();

                    success = success && Build(project, outputPath, targetFramework, builder, diagnostics);

                    allDiagnostics.AddRange(diagnostics);

                    if (diagnostics.Count > 0)
                    {
                        WriteDiagnostics(diagnostics);
                    }
                    else
                    {
                        createPackage = true;
                    }
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

            WriteSummary(allDiagnostics);

            if (createPackage)
            {
                using (var fs = File.Create(nupkg))
                {
                    builder.Save(fs);
                }

                System.Console.WriteLine("{0} -> {1}", project.Name, nupkg);
            }

            sw.Stop();

            System.Console.WriteLine("Compile took {0}ms", sw.ElapsedMilliseconds);
            return success;
        }

        private void WriteSummary(List<Diagnostic> allDiagnostics)
        {
            System.Console.WriteLine("{0} Errors, {1} Warnings", 
                allDiagnostics.Count(d => d.IsWarningAsError || d.Severity== DiagnosticSeverity.Error),
                allDiagnostics.Count(d => d.Severity== DiagnosticSeverity.Warning));
        }

        private void WriteDiagnostics(List<Diagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
            {
                string message = GetMessage(diagnostic);

                if (diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error)
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
            System.Console.WriteLine(message);
#endif
        }

        private void WriteWarning(string message)
        {
#if NET45
            WriteColor(message, ConsoleColor.Yellow);
#else
            System.Console.WriteLine(message);
#endif
        }

#if NET45
        private void WriteColor(string message, ConsoleColor color)
        {
            var old = System.Console.ForegroundColor;

            try
            {
                System.Console.ForegroundColor = color;
                System.Console.WriteLine(message);
            }
            finally
            {
                System.Console.ForegroundColor = old;
            }
        }
#endif

        private bool Build(KProject project, string outputPath, FrameworkName targetFramework, PackageBuilder builder, List<Diagnostic> diagnostics)
        {
            var roslynArtifactsProducer = PrepareCompiler(project, targetFramework);

            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(targetFramework);
            string targetPath = Path.Combine(outputPath, targetFrameworkFolder);

            var buildContext = new BuildContext(project.Name, targetFramework)
            {
                OutputPath = targetPath,
                PackageBuilder = builder,
                CopyDependencies = _buildOptions.CopyDependencies
            };

            return roslynArtifactsProducer.Build(buildContext, diagnostics);
        }

        private static RoslynArtifactsProducer PrepareCompiler(KProject project, FrameworkName targetFramework)
        {
            var projectDir = project.ProjectDirectory;
            var rootDirectory = DefaultHost.ResolveRootDirectory(projectDir);
            var globalAssemblyCache = new DefaultGlobalAssemblyCache();
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);

            var frameworkReferenceResolver = new FrameworkReferenceResolver(globalAssemblyCache);
            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();
            var compositeResourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });

            var nugetDependencyResolver = new NuGetDependencyResolver(projectDir);
            var gacDependencyExporter = new GacDependencyExporter(globalAssemblyCache);
            var compositeDependencyExporter = new CompositeDependencyExporter(new IDependencyExporter[] { 
                gacDependencyExporter, 
                nugetDependencyResolver 
            });

            var roslynCompiler = new RoslynCompiler(projectResolver,
                                                    NoopWatcher.Instance,
                                                    frameworkReferenceResolver,
                                                    compositeDependencyExporter);

            var projectReferenceResolver = new ProjectReferenceDependencyProvider(projectResolver);
            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] { 
                projectReferenceResolver, 
                nugetDependencyResolver 
            });

            dependencyWalker.Walk(project.Name, project.Version, targetFramework);

            var roslynArtifactsProducer = new RoslynArtifactsProducer(roslynCompiler,
                                                                      compositeResourceProvider,
                                                                      frameworkReferenceResolver,
                                                                      projectReferenceResolver.ResolvedDependencies);


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

        private static string GetPackagePath(KProject project, string outputPath)
        {
            return Path.Combine(outputPath, project.Name + "." + project.Version + ".nupkg");
        }

        private static string GetMessage(Diagnostic diagnostic)
        {
#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
            var formatter = DiagnosticFormatter.Instance;
#else
            var formatter = new DiagnosticFormatter();
#endif
            return formatter.Format(diagnostic);
        }
    }
}
