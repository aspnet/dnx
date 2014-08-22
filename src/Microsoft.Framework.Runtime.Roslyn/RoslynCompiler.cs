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
using System.Reflection;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynCompiler
    {
        private readonly ICache _cache;
        private readonly ICacheContextAccessor _cacheContextAccessor;
        private readonly IFileWatcher _watcher;
        private readonly IServiceProvider _services;

        public RoslynCompiler(ICache cache, 
                              ICacheContextAccessor cacheContextAccessor, 
                              IFileWatcher watcher, 
                              IServiceProvider services)
        {
            _cache = cache;
            _cacheContextAccessor = cacheContextAccessor;
            _watcher = watcher;
            _services = services;
        }

        public CompilationContext CompileProject(
            Project project,
            ILibraryKey target,
            IEnumerable<IMetadataReference> incomingReferences,
            IEnumerable<ISourceReference> incomingSourceReferences,
            IList<IMetadataReference> outgoingReferences)
        {
            var path = project.ProjectDirectory;
            var name = project.Name;

            var isMainAspect = String.IsNullOrEmpty(target.Aspect);
            var isPreprocessAspect = String.Equals(target.Aspect, "preprocess", StringComparison.OrdinalIgnoreCase);

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

            var compilationSettings = project.GetCompilationSettings(
                target.TargetFramework, 
                target.Configuration);

            var sourceFiles = Enumerable.Empty<String>();
            if (isMainAspect)
            {
                sourceFiles = project.SourceFiles;
            }
            else if (isPreprocessAspect)
            {
                sourceFiles = project.PreprocessSourceFiles;
            }

            IList<SyntaxTree> trees = GetSyntaxTrees(
                project, 
                sourceFiles, 
                compilationSettings, 
                incomingSourceReferences);

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

            if (isMainAspect)
            {
                var preprocessAssembly = Assembly.Load(new AssemblyName(project.Name + "!preprocess"));
                foreach (var preprocessType in preprocessAssembly.ExportedTypes)
                {
                    if (preprocessType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(ICompileModule)))
                    {
                        var module = (ICompileModule)ActivatorUtilities.CreateInstance(_services, preprocessType);
                        compilationContext.Modules.Add(module);
                    }
                }
            }

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
                                                 IEnumerable<string> sourceFiles,
                                                 CompilationSettings compilationSettings,
                                                 IEnumerable<ISourceReference> sourceReferences)
        {
            var trees = new List<SyntaxTree>();

            var parseOptions = new CSharpParseOptions(languageVersion: compilationSettings.LanguageVersion,
                                                      preprocessorSymbols: compilationSettings.Defines.AsImmutable());

            var dirs = new HashSet<string>();
            dirs.Add(project.ProjectDirectory);

            foreach (var sourcePath in sourceFiles)
            {
                dirs.Add(Path.GetDirectoryName(sourcePath));

                _watcher.WatchFile(sourcePath);

                var syntaxTree = CreateSyntaxTree(sourcePath, parseOptions);

                trees.Add(syntaxTree);
            }

            foreach (var sourceFileReference in sourceReferences.OfType<ISourceFileReference>())
            {
                var sourcePath = sourceFileReference.Path;

                dirs.Add(Path.GetDirectoryName(sourcePath));

                _watcher.WatchFile(sourcePath);

                var syntaxTree = CreateSyntaxTree(sourcePath, parseOptions);

                trees.Add(syntaxTree);
            }

            // Watch all directories
            var ctx = _cacheContextAccessor.Current;

            foreach (var d in dirs)
            {
                ctx.Monitor(new FileWriteTimeCacheDependency(d));
                _watcher.WatchDirectory(d, ".cs");
            }

            return trees;
        }

        private SyntaxTree CreateSyntaxTree(string sourcePath, CSharpParseOptions parseOptions)
        {
            // The cache key needs to take the parseOptions into account
            var cacheKey = sourcePath + string.Join(",", parseOptions.PreprocessorSymbolNames) + parseOptions.LanguageVersion;

            return _cache.Get<SyntaxTree>(cacheKey, ctx =>
            {
                ctx.Monitor(new FileWriteTimeCacheDependency(sourcePath));

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
            var metadata = _cache.Get<AssemblyMetadata>(path, ctx =>
            {
                ctx.Monitor(new FileWriteTimeCacheDependency(path));

                using (var stream = File.OpenRead(path))
                {
                    return AssemblyMetadata.CreateFromImageStream(stream);
                }
            });

            return new MetadataImageReference(metadata);
        }
    }
}
