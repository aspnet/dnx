// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NuGet;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynCompiler : IRoslynCompiler
    {
        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly IFileWatcher _watcher;
        private readonly IProjectResolver _projectResolver;
        private readonly MetadataFileReferenceFactory _metadataFileReferenceFactory;
        private readonly ProjectExportProvider _projectExportProvider;

        public RoslynCompiler(IProjectResolver projectResolver,
                              IFileWatcher watcher,
                              ILibraryExportProvider libraryExportProvider)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _libraryExportProvider = libraryExportProvider;
            _metadataFileReferenceFactory = new MetadataFileReferenceFactory();
            _projectExportProvider = new ProjectExportProvider(projectResolver);
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

            var exportProvider = new CachedCompilationLibraryExportProvider(this, compilationCache, _libraryExportProvider);
            var export = _projectExportProvider.GetProjectExport(exportProvider, name, targetFramework, out targetFramework);
            var metadataReferences = export.MetadataReferences;
            var exportedReferences = metadataReferences.Select(ConvertMetadataReference);

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);

            _watcher.WatchDirectory(path, ".cs");

            foreach (var directory in Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
            {
                _watcher.WatchDirectory(directory, ".cs");
            }

            var compilationSettings = project.GetCompilationSettings(targetFramework);

            IList<SyntaxTree> trees = GetSyntaxTrees(project, compilationSettings, export);

            var embeddedReferences = metadataReferences.OfType<IMetadataRawReference>()
                                                       .ToDictionary(a => a.Name, ConvertMetadataReference);

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
                                                 ILibraryExport export)
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

            foreach (var sourceReference in export.SourceReferences)
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
                var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);

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

        private MetadataReference ConvertMetadataReference(IMetadataReference metadataReference)
        {
            var roslynReference = metadataReference as IRoslynMetadataReference;

            if (roslynReference != null)
            {
                return roslynReference.MetadataReference;
            }

            var rawMetadataReference = metadataReference as IMetadataRawReference;

            if (rawMetadataReference != null)
            {
                return new MetadataImageReference(rawMetadataReference.Contents);
            }

            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null)
            {
                return _metadataFileReferenceFactory.GetMetadataReference(fileMetadataReference.Path);
            }

            throw new NotSupportedException();
        }

        private struct CachedCompilationLibraryExportProvider : ILibraryExportProvider
        {
            private readonly RoslynCompiler _compiler;
            private readonly IDictionary<string, CompilationContext> _compliationCache;
            private readonly ILibraryExportProvider _previous;

            public CachedCompilationLibraryExportProvider(RoslynCompiler compiler, IDictionary<string, CompilationContext> compliationCache, ILibraryExportProvider previous)
            {
                _compiler = compiler;
                _compliationCache = compliationCache;
                _previous = previous;
            }

            public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
            {
                return _compiler.GetLibraryExport(name, targetFramework, _compliationCache) ?? _previous.GetLibraryExport(name, targetFramework);
            }
        }
    }
}
