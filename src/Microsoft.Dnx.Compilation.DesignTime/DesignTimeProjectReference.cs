// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Compilation.DesignTime
{
    internal class DesignTimeProjectReference : IMetadataProjectReference
    {
        private readonly CompilationProjectContext _project;
        private readonly CompileResponse _response;

        public DesignTimeProjectReference(CompilationProjectContext project, CompileResponse response)
        {
            _project = project;
            _response = response;
        }

        public string Name { get { return _project.Target.Name; } }

        public string ProjectPath
        {
            get
            {
                return _project.ProjectDirectory;
            }
        }

        public DiagnosticResult GetDiagnostics()
        {
            bool hasErrors = _response.Diagnostics.HasErrors();
            return new DiagnosticResult(hasErrors, _response.Diagnostics);
        }

        public IList<ISourceReference> GetSources()
        {
            throw new NotSupportedException();
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            if (_response.Diagnostics.HasErrors())
            {
                throw new DesignTimeCompilationException(_response.Diagnostics);
            }

            byte[] assemblyBytes = null;
            byte[] pdbBytes = null;
            var resourceExtension = Path.GetExtension(assemblyName.Name);

            if (!string.IsNullOrEmpty(resourceExtension))
            {
                if (!string.IsNullOrEmpty(assemblyName.CultureName))
                {
                    _response.ResourcesBytes.TryGetValue(assemblyName.CultureName, out assemblyBytes);
                    _response.ResourcesBytes.TryGetValue(assemblyName.CultureName, out pdbBytes);
                }
            }
            else
            {
                if (_response.AssemblyPath != null)
                {
                    return loadContext.LoadFile(_response.AssemblyPath);
                }

                assemblyBytes = _response.AssemblyBytes;
                pdbBytes = _response.PdbBytes;
            }

            if (assemblyBytes == null)
            {
                return null;
            }

            if (pdbBytes == null)
            {
                 return loadContext.LoadStream(new MemoryStream(assemblyBytes), assemblySymbols: null);
            }

            return loadContext.LoadStream(new MemoryStream(assemblyBytes),
                                           new MemoryStream(pdbBytes));
        }

        public void EmitReferenceAssembly(Stream stream)
        {
            if (_response.AssemblyPath != null)
            {
                using (var fs = File.OpenRead(_response.AssemblyPath))
                {
                    fs.CopyTo(stream);
                }
            }
            else
            {
                stream.Write(_response.AssemblyBytes, 0, _response.AssemblyBytes.Length);
            }
        }

        public DiagnosticResult EmitAssembly(string outputPath)
        {
            throw new NotSupportedException();
        }
    }
}