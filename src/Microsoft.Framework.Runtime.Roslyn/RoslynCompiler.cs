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
    public class RoslynCompiler
    {
        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly IFileWatcher _watcher;
        private readonly IProjectResolver _projectResolver;
        private readonly MetadataFileReferenceFactory _metadataFileReferenceFactory;
        private readonly ProjectExportProviderHelper _projectExportProvider;

        public RoslynCompiler(IProjectResolver projectResolver,
                              IFileWatcher watcher,
                              ILibraryExportProvider libraryExportProvider)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _libraryExportProvider = libraryExportProvider;
            _metadataFileReferenceFactory = new MetadataFileReferenceFactory();
            _projectExportProvider = new ProjectExportProviderHelper(projectResolver);
        }

        public CompilationContext CompileProject(string name, FrameworkName targetFramework, string configuration)
        {
            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            string path = project.ProjectDirectory;

            _watcher.WatchProject(path);

            _watcher.WatchFile(project.ProjectFilePath);

            var export = _projectExportProvider.GetProjectExport(_libraryExportProvider, name, targetFramework, configuration, out targetFramework);
            var metadataReferences = export.MetadataReferences;
            var exportedReferences = metadataReferences.Select(ConvertMetadataReference);

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);

            _watcher.WatchDirectory(path, ".cs");

            foreach (var directory in Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
            {
                _watcher.WatchDirectory(directory, ".cs");
            }

            var compilationSettings = project.GetCompilationSettings(targetFramework, configuration);

            IList<SyntaxTree> trees = GetSyntaxTrees(project, compilationSettings, export);

            var embeddedReferences = metadataReferences.OfType<IMetadataEmbeddedReference>()
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

            var compilationContext = new CompilationContext(newCompilation,
                metadataReferences,
                assemblyNeutralTypeDiagnostics,
                project);

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

        private MetadataReference ConvertMetadataReference(IMetadataReference metadataReference)
        {
            var roslynReference = metadataReference as IRoslynMetadataReference;

            if (roslynReference != null)
            {
                return roslynReference.MetadataReference;
            }

            var embeddedReference = metadataReference as IMetadataEmbeddedReference;

            if (embeddedReference != null)
            {
                return new MetadataImageReference(embeddedReference.Contents);
            }

            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null)
            {
                return _metadataFileReferenceFactory.GetMetadataReference(fileMetadataReference.Path);
            }

            var projectReference = metadataReference as IMetadataProjectReference;
            if (projectReference != null)
            {
                using (var ms = new MemoryStream())
                {
                    projectReference.EmitReferenceAssembly(ms);

                    ms.Seek(0, SeekOrigin.Begin);

                    return new MetadataImageReference(ms);
                }
            }

            throw new NotSupportedException();
        }

        internal static IList<string> GetMessages(IEnumerable<Diagnostic> diagnostics)
        {
            var formatter = new DiagnosticFormatter();

            return diagnostics.Select(d => formatter.Format(d)).ToList();
        }

        internal static bool IsError(Diagnostic diagnostic)
        {
            return diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.IsWarningAsError;
        }
    }
}
