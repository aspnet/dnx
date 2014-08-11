using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    public class DesignTimeHostProjectReferenceProvider : IProjectReferenceProvider
    {
        private readonly IDesignTimeHostCompiler _compiler;

        public DesignTimeHostProjectReferenceProvider(IDesignTimeHostCompiler compiler)
        {
            _compiler = compiler;
        }

        public IMetadataProjectReference GetProjectReference(
            Project project,
            FrameworkName targetFramework,
            string configuration,
            Func<ILibraryExport> referenceResolver,
            IList<IMetadataReference> outgoingReferences)
        {
            var task = _compiler.Compile(new CompileRequest
            {
                ProjectPath = project.ProjectDirectory,
                Configuration = configuration,
                TargetFramework = targetFramework.ToString()
            });

            foreach (var embeddedReference in task.Result.EmbeddedReferences)
            {
                outgoingReferences.Add(new EmbeddedMetadataReference(embeddedReference.Key, embeddedReference.Value));
            }

            return new CompiledInMemoryProjectReference(task.Result);
        }

        private class CompiledInMemoryProjectReference : IMetadataProjectReference
        {
            private readonly CompileResponse _response;

            public CompiledInMemoryProjectReference(CompileResponse response)
            {
                _response = response;
            }

            public string Name { get { return _response.Name; } }

            public string ProjectPath
            {
                get
                {
                    return _response.ProjectPath;
                }
            }

            public IDiagnosticResult GetDiagnostics()
            {
                return new DiagnosticResult(_response.Errors.Any(), _response.Warnings, _response.Errors);
            }

            public IList<ISourceReference> GetSources()
            {
                return _response.Sources.Select(path => (ISourceReference)new SourceFileReference(path))
                                        .ToList();
            }

            public Assembly Load(IAssemblyLoaderEngine loaderEngine)
            {
                if (_response.AssemblyPath != null)
                {
                    return loaderEngine.LoadFile(_response.AssemblyPath);
                }

                if (_response.PdbBytes == null)
                {
                    return loaderEngine.LoadStream(new MemoryStream(_response.AssemblyBytes), pdbStream: null);
                }

                return loaderEngine.LoadStream(new MemoryStream(_response.AssemblyBytes),
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
}