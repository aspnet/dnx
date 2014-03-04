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
    public class RoslynCompiler : IRoslynCompiler
    {
        private readonly IDependencyExporter _dependencyResolver;
        private readonly IFrameworkReferenceResolver _resolver;
        private readonly IFileWatcher _watcher;
        private readonly IProjectResolver _projectResolver;

        public RoslynCompiler(IProjectResolver projectResolver,
                              IFileWatcher watcher,
                              IFrameworkReferenceResolver resolver,
                              IDependencyExporter dependencyExportResolver)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _resolver = resolver;
            _dependencyResolver = dependencyExportResolver;
        }

        public CompilationContext CompileProject(string name, FrameworkName targetFramework)
        {
            var compilationCache = new Dictionary<string, CompilationContext>();

            return Compile(name, targetFramework, compilationCache);
        }

        private CompilationContext Compile(string name, FrameworkName targetFramework, IDictionary<string, CompilationContext> compilationCache)
        {
            CompilationContext compilationContext;
            if (compilationCache.TryGetValue(name, out compilationContext))
            {
                return compilationContext;
            }

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

            if (project.Dependencies.Count > 0 ||
                targetFrameworkConfig.Dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                foreach (var dependency in project.Dependencies.Concat(targetFrameworkConfig.Dependencies))
                {
                    var dependencyExport = GetDependencyExport(dependency.Name, targetFramework, compilationCache) ??
                        _dependencyResolver.GetDependencyExport(dependency.Name, targetFramework);

                    if (dependencyExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                    }
                    else
                    {
                        exports.Add(dependencyExport);
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
            IList<CompilationContext> projectReferences;

            ExtractReferences(exports,
                              out exportedReferences,
                              out projectReferences,
                              out assemblyNeutralReferences);

            Trace.TraceInformation("[{0}]: Exported References {1}", GetType().Name, exportedReferences.Count);
            Trace.TraceInformation("[{0}]: Assembly Neutral References {1}", GetType().Name, assemblyNeutralReferences.Count);

            var references = new List<MetadataReference>();
            references.AddRange(exportedReferences);
            references.AddRange(_resolver.GetDefaultReferences(targetFramework));

            var compilation = CSharpCompilation.Create(
                name,
                compilationSettings.CompilationOptions,
                syntaxTrees: trees,
                references: references);

            var assemblyNeutralWorker = new AssemblyNeutralWorker(compilation, assemblyNeutralReferences);
            assemblyNeutralWorker.FindTypeCompilations(compilation.Assembly.GlobalNamespace);

            assemblyNeutralWorker.OrderTypeCompilations();
            var assemblyNeutralTypeDiagnostics = assemblyNeutralWorker.GenerateTypeCompilations();

            assemblyNeutralWorker.Generate();

            var newCompilation = assemblyNeutralWorker.Compilation;

            compilationContext = new CompilationContext
            {
                Compilation = newCompilation,
                Project = project,
                ProjectReferences = projectReferences
            };

            foreach (var t in assemblyNeutralWorker.TypeCompilations)
            {
                compilationContext.AssemblyNeutralReferences[t.AssemblyName] = new AssemblyNeutralMetadataReference(t);
            }

            // Add assembly neutral interfaces from 
            foreach (var t in assemblyNeutralReferences.Values)
            {
                compilationContext.AssemblyNeutralReferences[t.Name] = t;
            }

            compilationContext.Diagnostics.AddRange(assemblyNeutralTypeDiagnostics);

            compilationCache[name] = compilationContext;

            return compilationContext;
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

        public IEnumerable<IMetadataReference> GetReferences(string name, FrameworkName targetFramework)
        {
            var cache = new Dictionary<string, CompilationContext>();

            var export = GetDependencyExport(name, targetFramework, cache);

            if (export == null)
            {
                return null;
            }

            return export.MetadataReferences;
        }

        public RoslynDepenencyExport GetDependencyExport(string name, FrameworkName targetFramework, IDictionary<string, CompilationContext> compilationCache)
        {
            var compilationContext = Compile(name, targetFramework, compilationCache);

            if (compilationContext == null)
            {
                return null;
            }

            return MakeDependencyExport(compilationContext);
        }

        internal static RoslynDepenencyExport MakeDependencyExport(CompilationContext compilationContext)
        {
            var metadataReferences = new List<IMetadataReference>();
            var sourceReferences = new List<ISourceReference>();

            var metadataReference = compilationContext.Compilation.ToMetadataReference(embedInteropTypes: compilationContext.Project.EmbedInteropTypes);
            metadataReferences.Add(new MetadataReferenceWrapper(metadataReference));
            metadataReferences.AddRange(compilationContext.AssemblyNeutralReferences.Values);

            foreach (var sharedFile in compilationContext.Project.SharedFiles)
            {
                sourceReferences.Add(new SourceFileReference(sharedFile));
            }

            return new RoslynDepenencyExport(metadataReferences, sourceReferences, compilationContext);
        }

        private void ExtractReferences(List<IDependencyExport> dependencyExports,
                                       out IList<MetadataReference> references,
                                       out IList<CompilationContext> projectReferences,
                                       out IDictionary<string, AssemblyNeutralMetadataReference> assemblyNeutralReferences)
        {
            var paths = new HashSet<string>();
            references = new List<MetadataReference>();
            projectReferences = new List<CompilationContext>();
            assemblyNeutralReferences = new Dictionary<string, AssemblyNeutralMetadataReference>();

            foreach (var export in dependencyExports)
            {
                var roslynExport = export as RoslynDepenencyExport;

                // Roslyn exports have more information that we might want to flow to the 
                // original compilation
                if (roslynExport != null)
                {
                    projectReferences.Add(roslynExport.CompilationContext);
                }

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

                        references.Add(ConvertMetadataReference(reference));
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
