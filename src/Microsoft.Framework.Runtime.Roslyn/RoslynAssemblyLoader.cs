// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynAssemblyLoader : IAssemblyLoader
    {
        private readonly Dictionary<string, CompilationContext> _compilationCache = new Dictionary<string, CompilationContext>();

        private readonly ILibraryExportProvider _libraryExportProvider;
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private readonly IResourceProvider _resourceProvider;
        private readonly IApplicationEnvironment _applicationEnvironment;

        public RoslynAssemblyLoader(IAssemblyLoaderEngine loaderEngine,
                                    IApplicationEnvironment applicationEnvironment,
                                    ILibraryExportProvider libraryExportProvider)
        {
            _loaderEngine = loaderEngine;
            _applicationEnvironment = applicationEnvironment;
            _libraryExportProvider = libraryExportProvider;

            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();
            _resourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });
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

        private CompilationContext GetCompilationContext(string name, FrameworkName targetFramework, string configuration)
        {
            CompilationContext compilationContext;
            if (_compilationCache.TryGetValue(name, out compilationContext))
            {
                return compilationContext;
            }

            var export = _libraryExportProvider.GetLibraryExport(name, targetFramework, configuration);

            if (export == null)
            {
                return null;
            }

            // This has all transitive project references so we can just cache up front
            foreach (var projectReference in export.MetadataReferences.OfType<RoslynProjectReference>())
            {
                if (string.Equals(projectReference.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    compilationContext = projectReference.CompilationContext;
                }

                _compilationCache[projectReference.Name] = projectReference.CompilationContext;
            }

            return compilationContext;
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
                        RoslynCompiler.GetMessages(compilationContext.Diagnostics.Where(RoslynCompiler.IsError).Concat(result.Diagnostics)));
                }

                var errors = compilationContext.Diagnostics.Where(RoslynCompiler.IsError);
                if (errors.Any())
                {
                    throw new CompilationException(RoslynCompiler.GetMessages(errors));
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
    }
}
