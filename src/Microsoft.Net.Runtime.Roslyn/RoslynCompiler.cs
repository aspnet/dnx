using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using NuGet;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynCompiler : IRoslynCompiler, IDependencyExporter
    {
        private readonly IDependencyExporter _dependencyResolver;
        private readonly IFrameworkReferenceResolver _resolver;
        private readonly IFileWatcher _watcher;
        private readonly IProjectResolver _projectResolver;
        private readonly Dictionary<string, CompilationContext> _temporaryCache = new Dictionary<string, CompilationContext>();

        public RoslynCompiler(IProjectResolver projectResolver,
                              IFileWatcher watcher,
                              IFrameworkReferenceResolver resolver,
                              IDependencyExporter dependencyExportResolver)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _resolver = resolver;
            _dependencyResolver = new CompositeDependencyExporter(new[] { 
                this, 
                dependencyExportResolver });
        }

        public CompilationContext CompileProject(string name, FrameworkName targetFramework)
        {
            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            string path = project.ProjectDirectory;

            _watcher.WatchFile(project.ProjectFilePath);

            var targetFrameworkConfig = project.GetTargetFrameworkConfiguration(targetFramework);

            Trace.TraceInformation("[{0}]: Found project '{1}' framework={2}", GetType().Name, project.Name, targetFramework);

            var exports = new List<IDependencyExport>();
            var diagnostics = new List<Diagnostic>();
            var projectReferences = new List<CompilationContext>();

            if (project.Dependencies.Count > 0 ||
                targetFrameworkConfig.Dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                foreach (var dependency in project.Dependencies.Concat(targetFrameworkConfig.Dependencies))
                {
                    var dependencyExport = _dependencyResolver.GetDependencyExport(dependency.Name, targetFramework);

                    if (dependencyExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                    }
                    else
                    {
                        exports.Add(dependencyExport);

                        var roslynExport = dependencyExport as RoslynDepenencyExport;

                        // Roslyn exports have more information that we might want to flow to the 
                        // original compilation
                        if (roslynExport != null)
                        {
                            projectReferences.Add(roslynExport.CompilationContext);

                            diagnostics.AddRange(roslynExport.CompilationContext.Diagnostics);
                        }
                    }
                }

                dependencyStopWatch.Stop();
                Trace.TraceInformation("[{0}]: Completed loading dependencies for '{1}' in {2}ms", GetType().Name, project.Name, dependencyStopWatch.ElapsedMilliseconds);
            }

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);
            var sw = Stopwatch.StartNew();

            _watcher.WatchDirectory(path, ".cs");

            foreach (var directory in Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
            {
                _watcher.WatchDirectory(directory, ".cs");
            }

            var compilationSettings = project.GetCompilationSettings(targetFramework);

            IList<SyntaxTree> trees = GetSyntaxTrees(project, compilationSettings, exports);

            IDictionary<string, AssemblyNeutralMetadataReference> assemblyNeutralReferences;
            IList<MetadataReference> exportedReferences;

            ExtractReferences(exports, out exportedReferences, out assemblyNeutralReferences);

            var references = new List<MetadataReference>();
            references.AddRange(exportedReferences);
            references.AddRange(_resolver.GetDefaultReferences(targetFramework));

            var compilation = CSharpCompilation.Create(
                name,
                compilationSettings.CompilationOptions,
                syntaxTrees: trees,
                references: references);

            var assemblyNeutralWorker = new AssemblyNeutralWorker(compilation);
            assemblyNeutralWorker.FindTypeCompilations(compilation.GlobalNamespace);
            assemblyNeutralWorker.OrderTypeCompilations();
            var assemblyNeutralTypeDiagnostics = assemblyNeutralWorker.GenerateTypeCompilations();

            assemblyNeutralWorker.Generate(assemblyNeutralReferences);

            var oldCompilation = assemblyNeutralWorker.Compilation;
            var newCompilation = oldCompilation.WithReferences(
                oldCompilation.References.Concat(assemblyNeutralReferences.Values.Select(r => r.MetadataReference)));

            var context = new CompilationContext
            {
                Compilation = newCompilation,
                Project = project,
                AssemblyNeutralReferences = assemblyNeutralWorker.TypeCompilations.Select(t => new AssemblyNeutralMetadataReference(t)).ToList(),
                ProjectReferences = projectReferences
            };

            context.Diagnostics.AddRange(assemblyNeutralTypeDiagnostics);

            context.Diagnostics.AddRange(diagnostics);

            return context;
        }

        private IList<SyntaxTree> GetSyntaxTrees(Project project, CompilationSettings compilationSettings, List<IDependencyExport> exports)
        {
            var trees = new List<SyntaxTree>();

            var hasAssemblyInfo = false;

            var sourceFiles = project.SourceFiles.ToList();

            var parseOptions = new CSharpParseOptions(preprocessorSymbols: compilationSettings.Defines.AsImmutable());

            foreach (var sourcePath in sourceFiles)
            {
                if (!hasAssemblyInfo && Path.GetFileNameWithoutExtension(sourcePath).Equals("AssemblyInfo"))
                {
                    hasAssemblyInfo = true;
                }

                _watcher.WatchFile(sourcePath);
                trees.Add(CSharpSyntaxTree.ParseFile(sourcePath, parseOptions));
            }

            foreach (var sourceReference in exports.SelectMany(export => export.SourceReferences))
            {
                var sourceFileReference = sourceReference as ISourceFileReference;
                if (sourceFileReference != null)
                {
                    var sourcePath = sourceFileReference.Path;
                    _watcher.WatchFile(sourcePath);
                    trees.Add(CSharpSyntaxTree.ParseFile(sourcePath, parseOptions));
                }
            }

            if (!hasAssemblyInfo)
            {
                trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyVersion(\"" + project.Version.Version + "\")]"));
                trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyInformationalVersion(\"" + project.Version + "\")]"));
            }

            return trees;
        }

        public IDependencyExport GetDependencyExport(string name, FrameworkName targetFramework)
        {
            var compilationContext = CompileProject(name, targetFramework);

            if (compilationContext == null)
            {
                return null;
            }

            var metadataReferences = new List<IMetadataReference>();
            var sourceReferences = new List<ISourceReference>();

            var metadataReference = compilationContext.Compilation.ToMetadataReference(embedInteropTypes: compilationContext.Project.EmbedInteropTypes);
            metadataReferences.Add(new MetadataReferenceWrapper(metadataReference));
            metadataReferences.AddRange(compilationContext.AssemblyNeutralReferences);

            foreach (var sharedFile in compilationContext.Project.SharedFiles)
            {
                sourceReferences.Add(new SourceFileReference(sharedFile));
            }

            return new RoslynDepenencyExport(metadataReferences, sourceReferences, compilationContext);
        }

        private void ExtractReferences(List<IDependencyExport> dependencyExports,
                                       out IList<MetadataReference> references,
                                       out IDictionary<string, AssemblyNeutralMetadataReference> assemblyNeutralReferences)
        {
            var paths = new HashSet<string>();
            references = new List<MetadataReference>();
            assemblyNeutralReferences = new Dictionary<string, AssemblyNeutralMetadataReference>();

            foreach (var export in dependencyExports)
            {
                foreach (var reference in export.MetadataReferences)
                {
                    var fileMetadataReference = reference as IMetadataFileReference;

                    if (fileMetadataReference != null)
                    {
                        // Make sure nothing is duped
                        paths.Add(fileMetadataReference.Path);
                    }
                    else
                    {
                        var assemblyNeutralReference = reference as AssemblyNeutralMetadataReference;

                        if (assemblyNeutralReference != null)
                        {
                            assemblyNeutralReferences[assemblyNeutralReference.Name] = assemblyNeutralReference;
                        }
                        else
                        {
                            references.Add(ConvertMetadataReference(reference));
                        }
                    }
                }
            }

            // Now add the final set to the final set of references
            references.AddRange(paths.Select(path => new MetadataFileReference(path)));
        }

        private MetadataReference ConvertMetadataReference(IMetadataReference metadataReference)
        {
            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null)
            {
                return new MetadataFileReference(fileMetadataReference.Path);
            }

            var metadataReferenceWrapper = metadataReference as MetadataReferenceWrapper;

            if (metadataReferenceWrapper != null)
            {
                return metadataReferenceWrapper.MetadataReference;
            }

            throw new NotSupportedException();
        }

        private static DependencyExport CreateDependencyExport(MetadataReference metadataReference)
        {
            return new DependencyExport(new MetadataReferenceWrapper(metadataReference));
        }

        private static DependencyExport CreateDependencyExport(string assemblyLocation)
        {
            return CreateDependencyExport(new MetadataFileReference(assemblyLocation));
        }
    }
}
