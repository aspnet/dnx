// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Compilation
{
    internal class CompiledProjectMetadataReference : IMetadataProjectReference, IMetadataFileReference
    {
        private readonly ICompilationProject _project;
        private readonly string _assemblyPath;
        private readonly string _pdbPath;

        public CompiledProjectMetadataReference(ICompilationProject project, string assemblyPath, string pdbPath)
        {
            Name = project.Name;
            ProjectPath = project.ProjectFilePath;
            Path = assemblyPath;

            _project = project;
            _assemblyPath = assemblyPath;
            _pdbPath = pdbPath;
        }

        public string Name { get; private set; }

        public string ProjectPath { get; private set; }

        public string Path { get; private set; }

        public IDiagnosticResult GetDiagnostics()
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
            // LOUDO: put culturename into path
            return loadContext.LoadFile(_assemblyPath);
        }

        public void EmitReferenceAssembly(Stream stream)
        {
            using (var fs = File.OpenRead(_assemblyPath))
            {
                fs.CopyTo(stream);
            }
        }

        public IDiagnosticResult EmitAssembly(string outputPath)
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