// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Extensions.CompilationAbstractions
{
    public class CompiledProjectMetadataReference : IMetadataProjectReference, IMetadataFileReference
    {
        private readonly CompilationProjectContext _project;
        private readonly string _assemblyPath;
        private readonly string _pdbPath;

        public CompiledProjectMetadataReference(CompilationProjectContext project, string assemblyPath, string pdbPath)
        {
            Name = project.Target.Name;
            ProjectPath = project.ProjectFilePath;
            Path = assemblyPath;

            _project = project;
            _assemblyPath = assemblyPath;
            _pdbPath = pdbPath;
        }

        public string Name { get; }

        public string ProjectPath { get; }

        public string Path { get; }

        public DiagnosticResult GetDiagnostics()
        {
            return DiagnosticResult.Successful;
        }

        public IList<ISourceReference> GetSources()
        {
            return _project.Files.SourceFiles.Select(p => (ISourceReference)new SourceFileReference(p))
                                       .ToList();
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            return loadContext.LoadFile(_assemblyPath);
        }

        public void EmitReferenceAssembly(Stream stream)
        {
            using (var fs = File.OpenRead(_assemblyPath))
            {
                fs.CopyTo(stream);
            }
        }

        public DiagnosticResult EmitAssembly(string outputPath)
        {
            Copy(_assemblyPath, outputPath);
            Copy(_pdbPath, outputPath);

            return DiagnosticResult.Successful;
        }

        private static void Copy(string sourcePath, string outputPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            if (!File.Exists(sourcePath))
            {
                return;
            }

            Directory.CreateDirectory(outputPath);

            File.Copy(sourcePath, System.IO.Path.Combine(outputPath, System.IO.Path.GetFileName(sourcePath)), overwrite: true);
        }
    }
}