// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynProjectReference : IRoslynMetadataReference, IMetadataProjectReference
    {
        private static readonly IList<IMetadataEmbeddedReference> _emptyList = new IMetadataEmbeddedReference[0];

        private Lazy<IList<ResourceDescription>> _resources;
        private bool _beforeCompileCalled;

        public RoslynProjectReference(CompilationContext compilationContext)
        {
            CompilationContext = compilationContext;
            MetadataReference = compilationContext.Compilation.ToMetadataReference(embedInteropTypes: compilationContext.Project.EmbedInteropTypes);
            Name = compilationContext.Project.Name;
            _resources = new Lazy<IList<ResourceDescription>>(GetResources);
        }

        public CompilationContext CompilationContext { get; set; }

        public IList<ResourceDescription> Resources { get; set; }

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

        private void EnsureBeforeCompile()
        {
            if (_beforeCompileCalled)
            {
                return;
            }
            _beforeCompileCalled = true;
            var context = new BeforeCompileContext(this);
            foreach (var module in CompilationContext.Modules)
            {
                module.BeforeCompile(context);
            }
        }



        public IDiagnosticResult GetDiagnostics()
        {
            EnsureBeforeCompile();

            var diagnostics = CompilationContext.Diagnostics
                .Concat(CompilationContext.Compilation.GetDiagnostics());

            return CreateDiagnosticResult(success: true, diagnostics: diagnostics);
        }

        public IList<ISourceReference> GetSources()
        {
            EnsureBeforeCompile();

            // REVIEW: Raw sources?
            return CompilationContext.Compilation
                                     .SyntaxTrees
                                     .Select(t => t.FilePath)
                                     .Where(path => !string.IsNullOrEmpty(path))
                                     .Select(path => (ISourceReference)new SourceFileReference(path))
                                     .ToList();
        }

        public Assembly Load(IAssemblyLoaderEngine loaderEngine)
        {
            EnsureBeforeCompile();

            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
                IList<ResourceDescription> resources = _resources.Value;

                Trace.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, Name);

                var sw = Stopwatch.StartNew();

                EmitResult emitResult = null;

                if (PlatformHelper.IsMono)
                {
                    // Pdbs aren't supported on mono
                    emitResult = CompilationContext.Compilation.Emit(assemblyStream, manifestResources: resources);
                }
                else
                {
                    emitResult = CompilationContext.Compilation.Emit(assemblyStream, pdbStream: pdbStream, manifestResources: resources);
                }

                sw.Stop();

                Trace.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, Name, sw.ElapsedMilliseconds);

                var diagnostics = CompilationContext.Diagnostics.Concat(
                    emitResult.Diagnostics);

                var result = CreateDiagnosticResult(emitResult.Success, diagnostics);

                if (!result.Success)
                {
                    throw new CompilationException(result.Errors.ToList());
                }

                Assembly assembly = null;

                // Rewind the stream
                assemblyStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);

                if (pdbStream.Length == 0)
                {
                    assembly = loaderEngine.LoadStream(assemblyStream, pdbStream: null);
                }
                else
                {
                    assembly = loaderEngine.LoadStream(assemblyStream, pdbStream);
                }

                return assembly;
            }
        }

        public void EmitReferenceAssembly(Stream stream)
        {
            EnsureBeforeCompile();

            CompilationContext.Compilation.EmitMetadataOnly(stream);
        }

        public IDiagnosticResult EmitAssembly(string outputPath)
        {
            EnsureBeforeCompile();

            IList<ResourceDescription> resources = _resources.Value;

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
                    result = CompilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFilePath: null, pdbStream: null, xmlDocumentationStream: xmlDocStream, manifestResources: resources);
                }
                else
                {
                    result = CompilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFilePath: pdbPath, pdbStream: pdbStream, xmlDocumentationStream: xmlDocStream, manifestResources: resources);
                }

                sw.Stop();

                Trace.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, Name, sw.ElapsedMilliseconds);

                var diagnostics = new List<Diagnostic>(CompilationContext.Diagnostics);
                diagnostics.AddRange(result.Diagnostics);

                if (!result.Success ||
                    diagnostics.Any(IsError))
                {
                    return CreateDiagnosticResult(result.Success, diagnostics);
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

                return CreateDiagnosticResult(result.Success, diagnostics);
            }
        }

        private IList<ResourceDescription> GetResources()
        {
            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();

            var resourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });


            var sw = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Generating resources for {1}", GetType().Name, Name);

            var resources = resourceProvider.GetResources(CompilationContext.Project);

            sw.Stop();
            Trace.TraceInformation("[{0}]: Generated resources for {1} in {2}ms", GetType().Name, Name, sw.ElapsedMilliseconds);

            sw = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Resolving required assembly neutral references for {1}", GetType().Name, Name);

            var embeddedReferences = GetRequiredEmbeddedReferences();
            resources.AddEmbeddedReferences(embeddedReferences);

            Trace.TraceInformation("[{0}]: Resolved {1} required assembly neutral references for {2} in {3}ms",
                GetType().Name,
                embeddedReferences.Count,
                Name,
                sw.ElapsedMilliseconds);
            sw.Stop();

            return resources;
        }

        public IList<IMetadataEmbeddedReference> GetRequiredEmbeddedReferences()
        {
            var assemblyNeutralTypes = CompilationContext.MetadataReferences.OfType<IMetadataEmbeddedReference>()
                                                         .ToDictionary(r => r.Name);

            // No assembly neutral types so do nothing
            if (assemblyNeutralTypes.Count == 0)
            {
                return _emptyList;
            }

            // Walk the assembly neutral references and embed anything that we use
            // directly or indirectly
            var results = GetUsedReferences(assemblyNeutralTypes);


            // REVIEW: This should probably by driven by a property in the project metadata
            if (results.Count == 0)
            {
                // If nothing outgoing from this assembly, treat it like a carrier assembly
                // and embed everyting
                foreach (var a in assemblyNeutralTypes.Keys)
                {
                    results.Add(a);
                }
            }

            return results.Select(name => assemblyNeutralTypes[name])
                          .ToList();
        }

        private HashSet<string> GetUsedReferences(Dictionary<string, IMetadataEmbeddedReference> assemblies)
        {
            var results = new HashSet<string>();

            byte[] metadataBuffer = null;

            // First we need to emit just the metadata for this compilation
            using (var metadataStream = new MemoryStream())
            {
                var result = CompilationContext.Compilation.Emit(metadataStream);

                if (!result.Success)
                {
                    // We failed to emit metadata so do nothing since we're probably
                    // going to fail compilation anyways
                    return results;
                }

                // Store the buffer and close the stream
                metadataBuffer = metadataStream.ToArray();
            }

            var stack = new Stack<Tuple<string, byte[]>>();
            stack.Push(Tuple.Create((string)null, metadataBuffer));

            while (stack.Count > 0)
            {
                var top = stack.Pop();

                var assemblyName = top.Item1;

                if (!string.IsNullOrEmpty(assemblyName) &&
                    !results.Add(assemblyName))
                {
                    // Skip the reference if saw it already
                    continue;
                }

                var buffer = top.Item2;

                foreach (var reference in GetReferences(buffer))
                {
                    IMetadataEmbeddedReference embeddedReference;
                    if (assemblies.TryGetValue(reference, out embeddedReference))
                    {
                        stack.Push(Tuple.Create(reference, embeddedReference.Contents));
                    }
                }
            }

            return results;
        }

        private static IList<string> GetReferences(byte[] buffer)
        {
            var references = new List<string>();

            using (var stream = new MemoryStream(buffer))
            {
                var peReader = new PEReader(stream);

                var reader = peReader.GetMetadataReader();

                foreach (var a in reader.AssemblyReferences)
                {
                    var reference = reader.GetAssemblyReference(a);
                    var referenceName = reader.GetString(reference.Name);

                    references.Add(referenceName);
                }

                return references;
            }
        }

        private static DiagnosticResult CreateDiagnosticResult(bool success, IEnumerable<Diagnostic> diagnostics)
        {
            var formatter = new DiagnosticFormatter();

            var errors = diagnostics.Where(IsError)
                                .Select(d => formatter.Format(d)).ToList();

            var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                                  .Select(d => formatter.Format(d)).ToList();

            return new DiagnosticResult(success, warnings, errors);
        }

        private static bool IsError(Diagnostic diagnostic)
        {
            return diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.IsWarningAsError;
        }

        private class BeforeCompileContext : IBeforeCompileContext
        {
            private RoslynProjectReference roslynProjectReference;

            public BeforeCompileContext(RoslynProjectReference roslynProjectReference)
            {
                this.roslynProjectReference = roslynProjectReference;
            }

            public CSharpCompilation CSharpCompilation
            {
                get
                {
                    return roslynProjectReference.CompilationContext.Compilation;
                }
                set
                {
                    roslynProjectReference.CompilationContext.Compilation = value;
                }
            }

            public IList<Diagnostic> Diagnostics
            {
                get
                {
                    return roslynProjectReference.CompilationContext.Diagnostics;
                }
            }

            public IList<ResourceDescription> Resources
            {
                get
                {
                    return roslynProjectReference._resources.Value;
                }
            }
        }
    }
}
