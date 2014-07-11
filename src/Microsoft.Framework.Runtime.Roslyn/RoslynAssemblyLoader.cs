// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Loader;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynAssemblyLoader : IAssemblyLoader, ILibraryExportProvider
    {
        private readonly Dictionary<string, CompilationContext> _compilationCache = new Dictionary<string, CompilationContext>();

        private readonly IRoslynCompiler _compiler;
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private readonly IProjectResolver _projectResolver;
        private readonly IResourceProvider _resourceProvider;
        private readonly IApplicationEnvironment _applicationEnvironment;

        public RoslynAssemblyLoader(IAssemblyLoaderEngine loaderEngine,
                                    IApplicationEnvironment applicationEnvironment,
                                    IFileWatcher watcher,
                                    IProjectResolver projectResolver,
                                    ILibraryExportProvider dependencyExporter)
        {
            _loaderEngine = loaderEngine;
            _applicationEnvironment = applicationEnvironment;
            _projectResolver = projectResolver;

            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();

            _resourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });
            _compiler = new RoslynCompiler(projectResolver,
                                           watcher,
                                           dependencyExporter);
        }

        public Assembly Load(string assemblyName)
        {
            var compilationContext = GetCompilationContext(assemblyName, 
                                                           _applicationEnvironment.TargetFramework, 
                                                           _applicationEnvironment.Configuration);

            if (compilationContext == null)
            {
                return null;
            }

            var project = compilationContext.Project;
            var path = project.ProjectDirectory;
            var name = project.Name;

            var resources = _resourceProvider.GetResources(project);

            resources.AddEmbeddedReferences(compilationContext.GetRequiredEmbeddedReferences());

            return CompileInMemory(name, compilationContext, resources);
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework, string configuration)
        {
            var compliationContext = GetCompilationContext(name, targetFramework, configuration);

            if (compliationContext == null)
            {
                return null;
            }

            return compliationContext.GetLibraryExport();
        }

        private CompilationContext GetCompilationContext(string name, FrameworkName targetFramework, string configuration)
        {
            CompilationContext compilationContext;
            if (_compilationCache.TryGetValue(name, out compilationContext))
            {
                return compilationContext;
            }

            var context = _compiler.CompileProject(name, targetFramework, configuration);

            if (context == null)
            {
                return null;
            }

            CacheCompilation(context);

            return context;
        }

        private void CacheCompilation(CompilationContext context)
        {
            _compilationCache[context.Project.Name] = context;

            foreach (var projectReference in context.MetadataReferences.OfType<RoslynProjectReference>())
            {
                CacheCompilation(projectReference.CompilationContext);
            }
        }

        private Assembly CompileInMemory(string name, CompilationContext compilationContext, IEnumerable<ResourceDescription> resources)
        {
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
                Trace.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, name);

                var sw = Stopwatch.StartNew();

                EmitResult result = null;

                if (PlatformHelper.IsMono)
                {
                    result = compilationContext.Compilation.Emit(assemblyStream, manifestResources: resources);
                }
                else
                {
                    result = compilationContext.Compilation.Emit(assemblyStream, pdbStream: pdbStream, manifestResources: resources);
                }

                sw.Stop();

                Trace.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);

                if (!result.Success)
                {
                    throw new CompilationException(
                        GetErrors(compilationContext.Diagnostics.Where(IsError).Concat(result.Diagnostics)));
                }

                var errors = compilationContext.Diagnostics.Where(IsError);
                if (errors.Any())
                {
                    throw new CompilationException(GetErrors(errors));
                }

                Assembly assembly = null;

                // Rewind the stream
                assemblyStream.Seek(0, SeekOrigin.Begin);

                if (PlatformHelper.IsMono)
                {
                    // Pdbs aren't supported on mono
                    assembly = _loaderEngine.LoadStream(assemblyStream, pdbStream: null);
                }
                else
                {
                    // Rewind the pdb stream
                    pdbStream.Seek(0, SeekOrigin.Begin);

                    assembly = _loaderEngine.LoadStream(assemblyStream, pdbStream);
                }

                return assembly;
            }
        }

        private static IList<string> GetErrors(IEnumerable<Diagnostic> diagnostis)
        {
            var formatter = new DiagnosticFormatter();

            return diagnostis.Select(d => formatter.Format(d)).ToList();
        }

        private static bool IsError(Diagnostic diagnostic)
        {
            return diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.IsWarningAsError;
        }
    }
}
