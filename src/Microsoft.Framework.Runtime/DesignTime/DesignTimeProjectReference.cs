using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    internal class DesignTimeProjectReference : IMetadataProjectReference
    {
        private readonly Project _project;
        private readonly CompileResponse _response;

        public DesignTimeProjectReference(Project project, CompileResponse response)
        {
            _project = project;
            _response = response;
        }

        public string Name { get { return _project.Name; } }

        public string ProjectPath
        {
            get
            {
                return _project.ProjectDirectory;
            }
        }

        public IDiagnosticResult GetDiagnostics()
        {
            return new DiagnosticResult(_response.Errors.Any(), _response.Warnings, _response.Errors);
        }

        public IList<ISourceReference> GetSources()
        {
            throw new NotSupportedException();
        }

        public Assembly Load(IAssemblyLoadContext loadContext)
        {
            if(_response.Errors.Any())
            {
                throw new CompilationException(_response.Errors);
            }

            if (_response.AssemblyPath != null)
            {
                return loadContext.LoadFile(_response.AssemblyPath);
            }

            if (_response.PdbBytes == null)
            {
                return loadContext.LoadStream(new MemoryStream(_response.AssemblyBytes), assemblySymbols: null);
            }

            return loadContext.LoadStream(new MemoryStream(_response.AssemblyBytes),
                                           new MemoryStream(_response.PdbBytes));
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

        public IDiagnosticResult EmitAssembly(string outputPath)
        {
            throw new NotSupportedException();
        }
    }
}