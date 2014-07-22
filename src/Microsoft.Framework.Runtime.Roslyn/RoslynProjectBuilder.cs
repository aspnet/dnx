// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Framework.Runtime.FileSystem;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynProjectBuilder : IProjectBuilder
    {
        private readonly RoslynCompiler _compiler;
        private readonly IResourceProvider _resourceProvider;

        public RoslynProjectBuilder(IProjectResolver projectResolver,
                                    ILibraryExportProvider dependencyExporter)
        {
            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();

            _resourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });

            _compiler = new RoslynCompiler(projectResolver,
                                           NoopWatcher.Instance,
                                           dependencyExporter);
        }

        public IProjectBuildResult Build(string name,
                                         FrameworkName targetFramework,
                                         string configuration,
                                         string outputPath)
        {
            var compilationContext = _compiler.CompileProject(name, targetFramework, configuration);

            if (compilationContext == null)
            {
                return new RoslynBuildResult();
            }

            var project = compilationContext.Project;

            var resources = _resourceProvider.GetResources(project);

            resources.AddEmbeddedReferences(compilationContext.GetRequiredEmbeddedReferences());

            var diagnostics = new List<Diagnostic>();
            diagnostics.AddRange(compilationContext.Diagnostics);

            var success = CompileToDisk(name,
                                       targetFramework,
                                       configuration,
                                       outputPath,
                                       compilationContext,
                                       resources,
                                       diagnostics);

            var formatter = new DiagnosticFormatter();

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError)
                                .Select(d => formatter.Format(d)).ToList();

            var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                                  .Select(d => formatter.Format(d)).ToList();

            return new RoslynBuildResult(success, warnings, errors);
        }

        private bool CompileToDisk(
            string name,
            FrameworkName targetFramework,
            string configuration,
            string outputPath,
            CompilationContext compilationContext,
            IList<ResourceDescription> resources,
            List<Diagnostic> diagnostics)
        {
            var assemblyPath = Path.Combine(outputPath, name + ".dll");
            var pdbPath = Path.Combine(outputPath, name + ".pdb");
            var xmlDocPath = Path.Combine(outputPath, name + ".xml");

            // REVIEW: Memory bloat?
            using (var xmlDocStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
                Trace.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, name);

                var sw = Stopwatch.StartNew();

                EmitResult result = null;

                if (PlatformHelper.IsMono)
                {
                    // No pdb support yet
                    result = compilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFileName: null, pdbStream: null, xmlDocStream: xmlDocStream, manifestResources: resources);
                }
                else
                {
                    result = compilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFileName: pdbPath, pdbStream: pdbStream, xmlDocStream: xmlDocStream, manifestResources: resources);
                }

                sw.Stop();

                Trace.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);

                diagnostics.AddRange(result.Diagnostics);

                if (!result.Success)
                {
                    return false;
                }

                if (compilationContext.Diagnostics.Any(d => d.IsWarningAsError ||
                                                       d.Severity == DiagnosticSeverity.Error))
                {
                    return false;
                }

                // Ensure there's an output directory
                Directory.CreateDirectory(outputPath);

                assemblyStream.Position = 0;
                pdbStream.Position = 0;
                xmlDocStream.Position = 0;

                using (var assemblyFileStream = File.Create(assemblyPath))
                {
                    assemblyStream.CopyTo(assemblyFileStream);
                }

                using (var xmlDocFileStream = File.Create(xmlDocPath))
                {
                    xmlDocStream.CopyTo(xmlDocFileStream);
                }

                if (!PlatformHelper.IsMono)
                {
                    using (var pdbFileStream = File.Create(pdbPath))
                    {
                        pdbStream.CopyTo(pdbFileStream);
                    }
                }

                return true;
            }
        }

        private struct RoslynBuildResult : IProjectBuildResult
        {
            private readonly bool _success;
            private readonly IEnumerable<string> _warnings;
            private readonly IEnumerable<string> _errors;

            public RoslynBuildResult(bool success, IEnumerable<string> warnings, IEnumerable<string> errors)
            {
                _success = success;
                _warnings = warnings;
                _errors = errors;
            }

            public bool Success
            {
                get
                {
                    return _success;
                }
            }

            public IEnumerable<string> Warnings
            {
                get
                {
                    return _warnings;
                }
            }

            public IEnumerable<string> Errors
            {
                get
                {
                    return _errors;
                }
            }
        }
    }
}
