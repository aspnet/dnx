using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Net.Runtime.FileSystem;
using NuGet;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynCompiler : IRoslynCompiler
    {
        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly IFileWatcher _watcher;
        private readonly IProjectResolver _projectResolver;

        public RoslynCompiler(IProjectResolver projectResolver,
                              IFileWatcher watcher,
                              ILibraryExportProvider libraryExportProvider)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _libraryExportProvider = libraryExportProvider;
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

            var exports = new List<ILibraryExport>();

            var dependencies = project.Dependencies.Concat(targetFrameworkConfig.Dependencies)
                                                   .Select(d => d.Name)
                                                   .ToList();

            if (VersionUtility.IsDesktop(targetFramework))
            {
                // mscorlib is ok
                dependencies.Add("mscorlib");

                // TODO: Remove these references
                dependencies.Add("System");
                dependencies.Add("System.Core");
                dependencies.Add("Microsoft.CSharp");
            }


            if (dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                foreach (var dependency in dependencies)
                {
                    var libraryExport = GetLibraryExport(dependency, targetFramework, compilationCache) ??
                        _libraryExportProvider.GetLibraryExport(dependency, targetFramework);

                    if (libraryExport == null)
                    {
                        // TODO: Failed to resolve dependency so do something useful
                        Trace.TraceInformation("[{0}]: Failed to resolve dependency '{1}'", GetType().Name, dependency);
                    }
                    else
                    {
                        exports.Add(libraryExport);
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
            IList<IMetadataReference> metadataReferences;

            ExtractReferences(exports,
                              out exportedReferences,
                              out projectReferences,
                              out metadataReferences,
                              out assemblyNeutralReferences);

            Trace.TraceInformation("[{0}]: Exported References {1} {2}", GetType().Name, project.Name, exportedReferences.Count);
            Trace.TraceInformation("[{0}]: Assembly Neutral References {1} {2}", GetType().Name, project.Name, assemblyNeutralReferences.Count);

            var references = new List<MetadataReference>();
            references.AddRange(exportedReferences);

            var compilation = CSharpCompilation.Create(
                name,
                trees,
                references,
                compilationSettings.CompilationOptions);

            var assemblyNeutralWorker = new AssemblyNeutralWorker(compilation, assemblyNeutralReferences);
            assemblyNeutralWorker.FindTypeCompilations(compilation.Assembly.GlobalNamespace);

            assemblyNeutralWorker.OrderTypeCompilations();
            var assemblyNeutralTypeDiagnostics = assemblyNeutralWorker.GenerateTypeCompilations();

            assemblyNeutralWorker.Generate();

            foreach (var t in assemblyNeutralWorker.TypeCompilations)
            {
                metadataReferences.Add(new AssemblyNeutralMetadataReference(t));
            }

            var newCompilation = assemblyNeutralWorker.Compilation;

            compilationContext = new CompilationContext(newCompilation,
                metadataReferences,
                projectReferences,
                assemblyNeutralTypeDiagnostics,
                project);

            compilationCache[name] = compilationContext;

            return compilationContext;
        }

        private IList<SyntaxTree> GetSyntaxTrees(Project project,
                                                 CompilationSettings compilationSettings,
                                                 List<ILibraryExport> exports)
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

                var syntaxTree = CreateSyntaxTree(sourcePath, parseOptions);

                trees.Add(syntaxTree);
            }

            foreach (var sourceReference in exports.SelectMany(export => export.SourceReferences))
            {
                var sourceFileReference = sourceReference as ISourceFileReference;

                if (sourceFileReference != null)
                {
                    var sourcePath = sourceFileReference.Path;

                    _watcher.WatchFile(sourcePath);

                    var syntaxTree = CreateSyntaxTree(sourcePath, parseOptions);

                    trees.Add(syntaxTree);
                }
            }

            if (!hasAssemblyInfo)
            {
                trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyVersion(\"" + project.Version.Version + "\")]"));
                trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyInformationalVersion(\"" + project.Version + "\")]"));
            }

            return trees;
        }

        private static SyntaxTree CreateSyntaxTree(string sourcePath, CSharpParseOptions parseOptions)
        {
            using (var stream = File.OpenRead(sourcePath))
            {
                return CSharpSyntaxTree.ParseText(SourceText.From(stream), sourcePath, parseOptions);
            }
        }

        public RoslynLibraryExport GetLibraryExport(string name, FrameworkName targetFramework, IDictionary<string, CompilationContext> compilationCache)
        {
            var compilationContext = Compile(name, targetFramework, compilationCache);

            if (compilationContext == null)
            {
                return null;
            }

            return compilationContext.GetLibraryExport();
        }

        private void ExtractReferences(List<ILibraryExport> dependencyExports,
                                       out IList<MetadataReference> references,
                                       out IList<CompilationContext> projectReferences,
                                       out IList<IMetadataReference> metadataReferences,
                                       out IDictionary<string, AssemblyNeutralMetadataReference> assemblyNeutralReferences)
        {
            var used = new HashSet<string>();
            references = new List<MetadataReference>();
            projectReferences = new List<CompilationContext>();
            metadataReferences = new List<IMetadataReference>();
            assemblyNeutralReferences = new Dictionary<string, AssemblyNeutralMetadataReference>();

            foreach (var export in dependencyExports)
            {
                var roslynExport = export as RoslynLibraryExport;

                // Roslyn exports have more information that we might want to flow to the 
                // original compilation
                if (roslynExport != null)
                {
                    projectReferences.Add(roslynExport.CompilationContext);
                }

                foreach (var reference in export.MetadataReferences)
                {
                    if (!used.Add(reference.Name))
                    {
                        continue;
                    }

                    metadataReferences.Add(reference);

                    var fileMetadataReference = reference as IMetadataFileReference;

                    if (fileMetadataReference != null)
                    {
                        references.Add(MetadataFileReferenceFactory.CreateReference(fileMetadataReference.Path));
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
        }

        private MetadataReference ConvertMetadataReference(IMetadataReference metadataReference)
        {
            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null)
            {
                return MetadataFileReferenceFactory.CreateReference(fileMetadataReference.Path);
            }

            var roslynReference = metadataReference as RoslynMetadataReference;

            if (roslynReference != null)
            {
                return roslynReference.MetadataReference;
            }

            throw new NotSupportedException();
        }
    }
}
