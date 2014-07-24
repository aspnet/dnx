// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynProjectReference : IRoslynMetadataReference, IMetadataProjectReference
    {
        public RoslynProjectReference(CompilationContext compilationContext)
        {
            CompilationContext = compilationContext;
            MetadataReference = compilationContext.Compilation.ToMetadataReference(embedInteropTypes: compilationContext.Project.EmbedInteropTypes);
            Name = compilationContext.Project.Name;
        }

        public CompilationContext CompilationContext { get; set; }

        public MetadataReference MetadataReference
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            private set;
        }

        public string ProjectPath
        {
            get
            {
                return CompilationContext.Project.ProjectFilePath;
            }
        }

        public void EmitReferenceAssembly(Stream stream)
        {
            CompilationContext.Compilation.EmitMetadataOnly(stream);
        }

        public IProjectBuildResult GetDiagnostics()
        {
            var diagnostics = CompilationContext.Diagnostics
                .Concat(CompilationContext.Compilation.GetDiagnostics());

            return RoslynBuildResult.FromDiagnostics(success: true, diagnostics: diagnostics);
        }

        public IProjectBuildResult EmitAssembly(Stream assemblyStream, Stream pdbStream)
        {
            IList<ResourceDescription> resources = GetResources();

            Trace.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, Name);

            var sw = Stopwatch.StartNew();

            EmitResult result = null;

            if (PlatformHelper.IsMono)
            {
                result = CompilationContext.Compilation.Emit(assemblyStream, manifestResources: resources);
            }
            else
            {
                result = CompilationContext.Compilation.Emit(assemblyStream, pdbStream: pdbStream, manifestResources: resources);
            }

            sw.Stop();

            Trace.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, Name, sw.ElapsedMilliseconds);

            var diagnostics = CompilationContext.Diagnostics.Concat(
                result.Diagnostics);

            return RoslynBuildResult.FromDiagnostics(result.Success, diagnostics);
        }

        public IProjectBuildResult EmitAssembly(string outputPath)
        {
            IList<ResourceDescription> resources = GetResources();

            var assemblyPath = Path.Combine(outputPath, Name + ".dll");
            var pdbPath = Path.Combine(outputPath, Name + ".pdb");
            var xmlDocPath = Path.Combine(outputPath, Name + ".xml");

            // REVIEW: Memory bloat?
            using (var xmlDocStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
                Trace.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, Name);

                var sw = Stopwatch.StartNew();

                EmitResult result = null;

                if (PlatformHelper.IsMono)
                {
                    // No pdb support yet
                    result = CompilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFileName: null, pdbStream: null, xmlDocStream: xmlDocStream, manifestResources: resources);
                }
                else
                {
                    result = CompilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFileName: pdbPath, pdbStream: pdbStream, xmlDocStream: xmlDocStream, manifestResources: resources);
                }

                sw.Stop();

                Trace.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, Name, sw.ElapsedMilliseconds);

                var diagnostics = new List<Diagnostic>(CompilationContext.Diagnostics);
                diagnostics.AddRange(result.Diagnostics);

                if (!result.Success || 
                    diagnostics.Any(RoslynCompiler.IsError))
                {
                    return RoslynBuildResult.FromDiagnostics(result.Success, diagnostics);
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

                return RoslynBuildResult.FromDiagnostics(result.Success, diagnostics);
            }
        }

        private IList<ResourceDescription> GetResources()
        {
            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();

            var resourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });

            var resources = resourceProvider.GetResources(CompilationContext.Project);
            resources.AddEmbeddedReferences(CompilationContext.GetRequiredEmbeddedReferences());
            return resources;
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

            public static RoslynBuildResult FromDiagnostics(bool success, IEnumerable<Diagnostic> diagnostics)
            {
                var formatter = new DiagnosticFormatter();

                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError)
                                    .Select(d => formatter.Format(d)).ToList();

                var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                                      .Select(d => formatter.Format(d)).ToList();

                return new RoslynBuildResult(success, warnings, errors);
            }
        }
    }
}
