// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Runtime.FileSystem;
using NuGet;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynCompiler : IRoslynCompiler
    {
        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly IFileWatcher _watcher;
        private readonly IProjectResolver _projectResolver;
        private readonly MetadataFileReferenceFactory _metadataFileReferenceFactory;

        public RoslynCompiler(IProjectResolver projectResolver,
                              IFileWatcher watcher,
                              ILibraryExportProvider libraryExportProvider)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _libraryExportProvider = libraryExportProvider;
            _metadataFileReferenceFactory = new MetadataFileReferenceFactory();
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

            _watcher.WatchProject(path);

            _watcher.WatchFile(project.ProjectFilePath);

            var targetFrameworkConfig = project.GetTargetFrameworkConfiguration(targetFramework);

            // Update the target framework for compilation
            targetFramework = targetFrameworkConfig.FrameworkName ?? targetFramework;

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

            IList<MetadataReference> exportedReferences;
            IList<IMetadataReference> metadataReferences;

            ExtractReferences(exports,
                              targetFramework,
                              compilationCache,
                              out exportedReferences,
                              out metadataReferences);

            var embeddedReferences = metadataReferences.OfType<EmbeddedMetadataReference>()
                                                       .ToDictionary(a => a.Name);

            var references = new List<MetadataReference>();
            references.AddRange(exportedReferences);

            var compilation = CSharpCompilation.Create(
                name,
                trees,
                references,
                compilationSettings.CompilationOptions);

            var assemblyNeutralWorker = new AssemblyNeutralWorker(compilation, embeddedReferences);
            assemblyNeutralWorker.FindTypeCompilations(compilation.Assembly.GlobalNamespace);

            assemblyNeutralWorker.OrderTypeCompilations();
            var assemblyNeutralTypeDiagnostics = assemblyNeutralWorker.GenerateTypeCompilations();

            assemblyNeutralWorker.Generate();

            foreach (var t in assemblyNeutralWorker.TypeCompilations)
            {
                metadataReferences.Add(new EmbeddedMetadataReference(t));
            }

            Trace.TraceInformation("[{0}]: Exported References {1}", GetType().Name, metadataReferences.Count);

            var newCompilation = assemblyNeutralWorker.Compilation;

            newCompilation = ApplyVersionInfo(newCompilation, project);

            compilationContext = new CompilationContext(newCompilation,
                metadataReferences,
                assemblyNeutralTypeDiagnostics,
                project);

            compilationCache[name] = compilationContext;

            return compilationContext;
        }

        private static CSharpCompilation ApplyVersionInfo(CSharpCompilation compilation, Project project)
        {
            var emptyVersion = new Version(0, 0, 0, 0);

            // If the assembly version is empty then set the version
            if (compilation.Assembly.Identity.Version == emptyVersion)
            {
                return compilation.AddSyntaxTrees(new[]
                {
                    CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyVersion(\"" + project.Version.Version + "\")]"),
                    CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyInformationalVersion(\"" + project.Version + "\")]")
                });
            }

            return compilation;
        }

        private IList<SyntaxTree> GetSyntaxTrees(Project project,
                                                 CompilationSettings compilationSettings,
                                                 List<ILibraryExport> exports)
        {
            var trees = new List<SyntaxTree>();

            var sourceFiles = project.SourceFiles.ToList();

            var parseOptions = new CSharpParseOptions(languageVersion: compilationSettings.LanguageVersion,
                                                      preprocessorSymbols: compilationSettings.Defines.AsImmutable());

            foreach (var sourcePath in sourceFiles)
            {
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

            return trees;
        }

        private static SyntaxTree CreateSyntaxTree(string sourcePath, CSharpParseOptions parseOptions)
        {
            using (var stream = File.OpenRead(sourcePath))
            {
#if NET45
                var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
#else
                var sourceText = SourceText.From(stream);
#endif
                return CSharpSyntaxTree.ParseText(sourceText, options: parseOptions, path: sourcePath);
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
                                       FrameworkName targetFramework,
                                       IDictionary<string, CompilationContext> compilationCache,
                                       out IList<MetadataReference> references,
                                       out IList<IMetadataReference> metadataReferences)
        {
            var used = new HashSet<string>();
            references = new List<MetadataReference>();
            metadataReferences = new List<IMetadataReference>();

            foreach (var export in dependencyExports)
            {
                ProcessExport(export,
                              targetFramework,
                              compilationCache,
                              references,
                              metadataReferences,
                              used);
            }
        }

        private void ProcessExport(ILibraryExport export,
                                   FrameworkName targetFramework,
                                   IDictionary<string, CompilationContext> compilationCache,
                                   IList<MetadataReference> references,
                                   IList<IMetadataReference> metadataReferences,
                                   HashSet<string> used)
        {
            ExpandEmbeddedReferences(export.MetadataReferences);

            foreach (var reference in export.MetadataReferences)
            {
                var unresolvedReference = reference as UnresolvedMetadataReference;

                if (unresolvedReference != null)
                {
                    // Try to resolve the unresolved references
                    var compilationExport = GetLibraryExport(unresolvedReference.Name, targetFramework, compilationCache);

                    if (compilationExport != null)
                    {
                        ProcessExport(compilationExport, targetFramework, compilationCache, references, metadataReferences, used);
                    }
                }
                else
                {
                    if (!used.Add(reference.Name))
                    {
                        continue;
                    }

                    metadataReferences.Add(reference);
                    references.Add(ConvertMetadataReference(reference));
                }
            }
        }

        private void ExpandEmbeddedReferences(IList<IMetadataReference> references)
        {
            var otherReferences = new List<IMetadataReference>();

            foreach (var reference in references)
            {
                var fileReference = reference as IMetadataFileReference;

                if (fileReference != null)
                {
                    using (var fileStream = File.OpenRead(fileReference.Path))
                    using (var reader = new PEReader(fileStream))
                    {
                        otherReferences.AddRange(reader.GetEmbeddedReferences());
                    }
                }
            }

            references.AddRange(otherReferences);
        }

        private MetadataReference ConvertMetadataReference(IMetadataReference metadataReference)
        {
            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null)
            {
                return _metadataFileReferenceFactory.GetMetadataReference(fileMetadataReference.Path);
            }

            var roslynReference = metadataReference as IRoslynMetadataReference;

            if (roslynReference != null)
            {
                return roslynReference.MetadataReference;
            }

            throw new NotSupportedException();
        }
    }
}
