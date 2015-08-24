// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class RoslynProjectReference : IRoslynMetadataReference, IMetadataProjectReference
    {
        private static Lazy<bool> _supportsPdbGeneration = new Lazy<bool>(SupportsPdbGeneration);

        public RoslynProjectReference(CompilationContext compilationContext)
        {
            CompilationContext = compilationContext;
            MetadataReference = compilationContext.Compilation.ToMetadataReference(embedInteropTypes: compilationContext.Project.EmbedInteropTypes);
            Name = compilationContext.Project.Target.Name;
        }

        public CompilationContext CompilationContext { get; private set; }

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

        public DiagnosticResult GetDiagnostics()
        {
            var diagnostics = CompilationContext.Diagnostics
                .Concat(CompilationContext.Compilation.GetDiagnostics());

            return CreateDiagnosticResult(success: true, diagnostics: diagnostics, targetFramework: CompilationContext.ProjectContext.TargetFramework);
        }

        public IList<ISourceReference> GetSources()
        {
            // REVIEW: Raw sources?
            return CompilationContext.Compilation
                                     .SyntaxTrees
                                     .Select(t => t.FilePath)
                                     .Where(path => !string.IsNullOrEmpty(path))
                                     .Select(path => (ISourceReference)new SourceFileReference(path))
                                     .ToList();
        }

        public EmitResult EmitResourceAssembly(
            AssemblyName assemblyName,
            IEnumerable<ResourceDescriptor> resourceDescriptors,
            AfterCompileContext afterCompileContext)
        {
            var resources = resourceDescriptors
                .Select(res => new ResourceDescription(res.Name, res.StreamFactory, isPublic: true));

            var mainCompilation = afterCompileContext.Compilation;
            afterCompileContext.Compilation = CSharpCompilation.Create(
                assemblyName.Name,
                options: mainCompilation.Options);

            Logger.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, Name);

            var sw = Stopwatch.StartNew();

            var emitResult = afterCompileContext.Compilation.Emit(
                    afterCompileContext.AssemblyStream,
                    manifestResources: resources);

            sw.Stop();

            Logger.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, Name, sw.ElapsedMilliseconds);

            afterCompileContext.Diagnostics = CompilationContext.Diagnostics
                .Concat(emitResult.Diagnostics)
                .ToList();

            return emitResult;
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
                var afterCompileContext = new AfterCompileContext
                {
                    ProjectContext = CompilationContext.ProjectContext,
                    Compilation = CompilationContext.Compilation,
                    AssemblyStream = assemblyStream,
                    SymbolStream = pdbStream
                };

                EmitResult emitResult = null;

                if (!string.IsNullOrEmpty(assemblyName.CultureName))
                {
                    var resourcesForCulture = ResourcesForCulture.GetResourcesForCulture(assemblyName.CultureName ?? string.Empty, CompilationContext.Resources);
                    if (resourcesForCulture == null)
                    {
                        return null;
                    }
                    afterCompileContext.SymbolStream = null;
                    emitResult = EmitResourceAssembly(assemblyName, resourcesForCulture, afterCompileContext);
                }
                else
                {
                    var resourcesForCulture = ResourcesForCulture.GetResourcesForCulture(assemblyName.CultureName ?? string.Empty, CompilationContext.Resources);
                    if (resourcesForCulture == null)
                    {
                        // No resources is fine for a main assembly
                        resourcesForCulture = Enumerable.Empty<ResourceDescriptor>();
                    }
                    var resources = resourcesForCulture
                        .Select(res => new ResourceDescription(res.Name, res.StreamFactory, isPublic: true));

                    Logger.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, Name);

                    var sw = Stopwatch.StartNew();

                    if (_supportsPdbGeneration.Value)
                    {
                        emitResult = CompilationContext.Compilation.Emit(assemblyStream, pdbStream: pdbStream, manifestResources: resources);
                    }
                    else
                    {
                        Logger.TraceWarning("PDB generation is not supported on this platform");
                        emitResult = CompilationContext.Compilation.Emit(assemblyStream, manifestResources: resources);
                    }

                    afterCompileContext.Diagnostics = CompilationContext.Diagnostics.Concat(emitResult.Diagnostics).ToList();

                    sw.Stop();

                    Logger.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, Name, sw.ElapsedMilliseconds);
                }

                if (!Path.GetExtension(assemblyName.Name).Contains("resources"))
                {
                    foreach (var m in CompilationContext.Modules)
                    {
                        m.AfterCompile(afterCompileContext);
                    }
                }

                if (!emitResult.Success ||
                    afterCompileContext.Diagnostics.Any(RoslynDiagnosticUtilities.IsError))
                {
                    throw new RoslynCompilationException(afterCompileContext.Diagnostics, CompilationContext.ProjectContext.TargetFramework);
                }

                Assembly assembly = null;

                // If this is null it'll fail anyways, just don't blow up with
                // a null reference
                if (afterCompileContext.AssemblyStream != null)
                {
                    afterCompileContext.AssemblyStream.Position = 0;
                }

                if (afterCompileContext.SymbolStream == null ||
                    afterCompileContext.SymbolStream.Length == 0)
                {
                    assembly = loadContext.LoadStream(afterCompileContext.AssemblyStream, assemblySymbols: null);
                }
                else
                {
                    afterCompileContext.SymbolStream.Position = 0;

                    assembly = loadContext.LoadStream(afterCompileContext.AssemblyStream, afterCompileContext.SymbolStream);
                }

                return assembly;
            }
        }

        public void EmitReferenceAssembly(Stream stream)
        {
            var emitOptions = new EmitOptions(metadataOnly: true);
            CompilationContext.Compilation.Emit(stream, options: emitOptions);
        }

        public DiagnosticResult EmitAssembly(string outputPath)
        {
            var resources = Enumerable.Empty<ResourceDescription>();
            EmitResources(outputPath, out resources);

            var assemblyPath = Path.Combine(outputPath, Name + ".dll");
            var pdbPath = Path.Combine(outputPath, Name + ".pdb");
            var xmlDocPath = Path.Combine(outputPath, Name + ".xml");

            // REVIEW: Memory bloat?

            using (var xmlDocStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            using (var win32resStream = CompilationContext.Compilation.CreateDefaultWin32Resources(
                versionResource: true,
                noManifest: false,
                manifestContents: null,
                iconInIcoFormat: null))
            {
                // The default win32resStream extracted from compilation represents a Win32 applicaiton manifest.
                // It enables the assmebly information to be viewed in Windows Explorer.

                Logger.TraceInformation("[{0}]: Emitting assembly for {1}", GetType().Name, Name);

                var sw = Stopwatch.StartNew();

                EmitResult emitResult = null;

                if (_supportsPdbGeneration.Value)
                {
                    var options = new EmitOptions(pdbFilePath: pdbPath);
                    emitResult = CompilationContext.Compilation.Emit(
                        assemblyStream,
                        pdbStream: pdbStream,
                        xmlDocumentationStream: xmlDocStream,
                        win32Resources: win32resStream,
                        manifestResources: resources,
                        options: options);
                }
                else
                {
                    Logger.TraceWarning("PDB generation is not supported on this platform");
                    emitResult = CompilationContext.Compilation.Emit(
                        assemblyStream,
                        xmlDocumentationStream: xmlDocStream,
                        manifestResources: resources,
                        win32Resources: win32resStream);
                }

                sw.Stop();

                Logger.TraceInformation("[{0}]: Emitted {1} in {2}ms", GetType().Name, Name, sw.ElapsedMilliseconds);

                var diagnostics = CompilationContext.Diagnostics.Concat(
                    emitResult.Diagnostics);

                var afterCompileContext = new AfterCompileContext
                {
                    ProjectContext = CompilationContext.ProjectContext,
                    Compilation = CompilationContext.Compilation,
                    Diagnostics = new List<Diagnostic>(diagnostics),
                    AssemblyStream = assemblyStream,
                    SymbolStream = pdbStream,
                    XmlDocStream = xmlDocStream
                };

                foreach (var m in CompilationContext.Modules)
                {
                    m.AfterCompile(afterCompileContext);
                }

                if (!emitResult.Success ||
                    afterCompileContext.Diagnostics.Any(RoslynDiagnosticUtilities.IsError))
                {
                    return CreateDiagnosticResult(emitResult.Success, afterCompileContext.Diagnostics,
                        CompilationContext.ProjectContext.TargetFramework);
                }

                // Ensure there's an output directory
                Directory.CreateDirectory(outputPath);

                if (afterCompileContext.AssemblyStream != null)
                {
                    afterCompileContext.AssemblyStream.Position = 0;

                    using (var assemblyFileStream = File.Create(assemblyPath))
                    {
                        afterCompileContext.AssemblyStream.CopyTo(assemblyFileStream);
                    }
                }

                if (afterCompileContext.XmlDocStream != null)
                {
                    afterCompileContext.XmlDocStream.Position = 0;
                    using (var xmlDocFileStream = File.Create(xmlDocPath))
                    {
                        afterCompileContext.XmlDocStream.CopyTo(xmlDocFileStream);
                    }
                }

                if (_supportsPdbGeneration.Value)
                {
                    if (afterCompileContext.SymbolStream != null)
                    {
                        afterCompileContext.SymbolStream.Position = 0;

                        using (var pdbFileStream = File.Create(pdbPath))
                        {
                            afterCompileContext.SymbolStream.CopyTo(pdbFileStream);
                        }
                    }
                }

                return CreateDiagnosticResult(emitResult.Success, afterCompileContext.Diagnostics,
                        CompilationContext.ProjectContext.TargetFramework);
            }
        }

        private DiagnosticResult EmitResources(string outputPath, out IEnumerable<ResourceDescription> resources)
        {
            resources = Enumerable.Empty<ResourceDescription>();

            foreach (var resourceGrouping in CompilationContext.Resources
                .GroupBy(ResourcesForCulture.GetResourceCultureName, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(resourceGrouping.Key))
                {
                    resources = resourceGrouping
                        .Select(res => new ResourceDescription(res.Name, res.StreamFactory, isPublic: true))
                        .ToList();
                }
                else
                {
                    using (var resourceAssemblyStream = new MemoryStream())
                    {
                        var resourceAfterCompileContext = new AfterCompileContext
                        {
                            ProjectContext = CompilationContext.ProjectContext,
                            Compilation = CompilationContext.Compilation,
                            AssemblyStream = resourceAssemblyStream,
                        };
                        var resourceAssemblyName = new AssemblyName(Name);
                        var emitResult = EmitResourceAssembly(
                            resourceAssemblyName,
                            resourceGrouping,
                            resourceAfterCompileContext);

                        resourceAfterCompileContext.Diagnostics = new List<Diagnostic>(emitResult.Diagnostics);

                        foreach (var m in CompilationContext.Modules)
                        {
                            m.AfterCompile(resourceAfterCompileContext);
                        }

                        if (!emitResult.Success ||
                            resourceAfterCompileContext.Diagnostics.Any(RoslynDiagnosticUtilities.IsError))
                        {
                            return CreateDiagnosticResult(
                                emitResult.Success,
                                resourceAfterCompileContext.Diagnostics,
                                CompilationContext.ProjectContext.TargetFramework);
                        }

                        // Ensure there's an output directory
                        Directory.CreateDirectory(Path.Combine(
                            outputPath,
                            resourceGrouping.Key));

                        if (resourceAfterCompileContext.AssemblyStream != null)
                        {
                            resourceAfterCompileContext.AssemblyStream.Position = 0;

                            using (var assemblyFileStream = File.Create(Path.Combine(
                                outputPath,
                                resourceGrouping.Key,
                                Name + ".resources.dll")))
                            {
                                resourceAfterCompileContext.AssemblyStream.CopyTo(assemblyFileStream);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static DiagnosticResult CreateDiagnosticResult(
            bool success,
            IEnumerable<Diagnostic> diagnostics,
            FrameworkName targetFramework)
        {
            var issues = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error);
            return new DiagnosticResult(success, issues.Select(d => d.ToDiagnosticMessage(targetFramework)));
        }

        private static bool SupportsPdbGeneration()
        {
            try
            {
                if (!RuntimeEnvironmentHelper.IsWindows)
                {
                    return false;
                }

                // Check for the pdb writer component that roslyn uses to generate pdbs
                const string SymWriterGuid = "0AE2DEB0-F901-478b-BB9F-881EE8066788";

                var type = Marshal.GetTypeFromCLSID(new Guid(SymWriterGuid));

                if (type != null)
                {
                    return Activator.CreateInstance(type) != null;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
