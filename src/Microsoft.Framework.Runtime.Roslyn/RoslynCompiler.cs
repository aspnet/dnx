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

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynCompiler
    {
        private readonly ICache _cache;
        private readonly IFileWatcher _watcher;

        public RoslynCompiler(ICache cache, IFileWatcher watcher)
        {
            _cache = cache;
            _watcher = watcher;
        }

        public CompilationContext CompileProject(
            Project project,
            FrameworkName targetFramework,
            string configuration,
            IEnumerable<IMetadataReference> incomingReferences,
            IEnumerable<ISourceReference> incomingSourceReferences,
            IList<IMetadataReference> outgoingReferences)
        {
            var path = project.ProjectDirectory;
            var name = project.Name;

            _watcher.WatchProject(path);

            _watcher.WatchFile(project.ProjectFilePath);

            var exportedReferences = incomingReferences.Select(ConvertMetadataReference);

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);
            var sw = Stopwatch.StartNew();

            _watcher.WatchDirectory(path, ".cs");

            foreach (var directory in Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
            {
                _watcher.WatchDirectory(directory, ".cs");
            }

            var compilationSettings = project.GetCompilationSettings(targetFramework, configuration);

            IList<SyntaxTree> trees = GetSyntaxTrees(project, compilationSettings, incomingSourceReferences);

            var embeddedReferences = incomingReferences.OfType<IMetadataEmbeddedReference>()
                                                       .ToDictionary(a => a.Name, ConvertMetadataReference);

            var references = new List<MetadataReference>();
            references.AddRange(exportedReferences);

            var compilation = CSharpCompilation.Create(
                name,
                trees,
                references,
                compilationSettings.CompilationOptions);

            var aniSw = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Scanning '{1}' for assembly neutral interfaces", GetType().Name, name);

            var assemblyNeutralWorker = new AssemblyNeutralWorker(compilation, embeddedReferences);
            assemblyNeutralWorker.FindTypeCompilations(compilation.Assembly.GlobalNamespace);

            assemblyNeutralWorker.OrderTypeCompilations();
            var assemblyNeutralTypeDiagnostics = assemblyNeutralWorker.GenerateTypeCompilations();

            assemblyNeutralWorker.Generate();

            aniSw.Stop();
            Trace.TraceInformation("[{0}]: Found {1} assembly neutral interfaces for '{2}' in {3}ms", GetType().Name, assemblyNeutralWorker.TypeCompilations.Count(), name, aniSw.ElapsedMilliseconds);

            foreach (var t in assemblyNeutralWorker.TypeCompilations)
            {
                outgoingReferences.Add(new EmbeddedMetadataReference(t));
            }

            var newCompilation = assemblyNeutralWorker.Compilation;

            newCompilation = ApplyVersionInfo(newCompilation, project);

            var compilationContext = new CompilationContext(newCompilation,
                incomingReferences.Concat(outgoingReferences).ToList(),
                assemblyNeutralTypeDiagnostics,
                project);

            sw.Stop();
            Trace.TraceInformation("[{0}]: Compiled '{1}' in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);

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
                                                 IEnumerable<ISourceReference> sourceReferences)
        {
            var trees = new List<SyntaxTree>();

            var parseOptions = new CSharpParseOptions(languageVersion: compilationSettings.LanguageVersion,
                                                      preprocessorSymbols: compilationSettings.Defines.AsImmutable());

            foreach (var sourcePath in project.SourceFiles)
            {
                _watcher.WatchFile(sourcePath);

                var syntaxTree = CreateSyntaxTree(sourcePath, parseOptions);

                trees.Add(syntaxTree);
            }

            foreach (var sourceFileReference in sourceReferences.OfType<ISourceFileReference>())
            {
                var sourcePath = sourceFileReference.Path;

                _watcher.WatchFile(sourcePath);

                var syntaxTree = CreateSyntaxTree(sourcePath, parseOptions);

                trees.Add(syntaxTree);
            }

            return trees;
        }

        private SyntaxTree CreateSyntaxTree(string sourcePath, CSharpParseOptions parseOptions)
        {
            return _cache.Get<SyntaxTree>(sourcePath, ctx =>
            {
                ctx.Monitor(new FileWriteTimeChangedToken(sourcePath));

                using (var stream = File.OpenRead(sourcePath))
                {
                    var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);

                    return CSharpSyntaxTree.ParseText(sourceText, options: parseOptions, path: sourcePath);
                }
            });
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
                return GetMetadataReference(fileMetadataReference.Path);
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

        private MetadataReference GetMetadataReference(string path)
        {
            return _cache.Get<MetadataReference>(path, ctx =>
            {
                ctx.Monitor(new FileWriteTimeChangedToken(path));

                using (var stream = File.OpenRead(path))
                {
                    return new MetadataImageReference(stream);
                }
            });
        }
    }
}
