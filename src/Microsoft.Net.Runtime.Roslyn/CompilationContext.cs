using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class CompilationContext
    {
        private RoslynLibraryExport _roslynLibraryExport;

        /// <summary>
        /// The project associated with this compilation
        /// </summary>
        public Project Project { get; private set; }

        // Processed information
        public CSharpCompilation Compilation { get; private set; }
        public IList<Diagnostic> Diagnostics { get; private set; }

        public IList<IMetadataReference> MetadataReferences { get; private set; }
        public IList<CompilationContext> ProjectReferences { get; private set; }

        public CompilationContext(CSharpCompilation compilation,
                                  IList<IMetadataReference> metadataReferences,
                                  IList<CompilationContext> projectReferences,
                                  IList<Diagnostic> diagnostics,
                                  Project project)
        {
            Compilation = compilation;
            MetadataReferences = metadataReferences;
            ProjectReferences = projectReferences;
            Diagnostics = diagnostics;
            Project = project;
        }

        public RoslynLibraryExport GetLibraryExport()
        {
            if (_roslynLibraryExport == null)
            {
                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();

                // Compilation reference
                var metadataReference = Compilation.ToMetadataReference(embedInteropTypes: Project.EmbedInteropTypes);
                metadataReferences.Add(new RoslynMetadataReference(Project.Name, metadataReference));

                // Other references
                metadataReferences.AddRange(MetadataReferences);

                // Shared sources
                foreach (var sharedFile in Project.SharedFiles)
                {
                    sourceReferences.Add(new SourceFileReference(sharedFile));
                }

                _roslynLibraryExport = new RoslynLibraryExport(metadataReferences, sourceReferences, this);
            }

            return _roslynLibraryExport;
        }

        public void PopulateAssemblyNeutralResources(IList<ResourceDescription> resources)
        {
            var assemblyNeutralTypes = MetadataReferences.OfType<EmbeddedMetadataReference>()
                                                         .ToDictionary(r => r.Name, r => r.OutputStream);

            // No assembly neutral types so do nothing
            if (assemblyNeutralTypes.Count == 0)
            {
                return;
            }

            Trace.TraceInformation("Assembly Neutral References {0}", assemblyNeutralTypes.Count);
            var sw = Stopwatch.StartNew();

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

            foreach (var reference in results)
            {
                var stream = assemblyNeutralTypes[reference];

                resources.Add(new ResourceDescription(reference + ".dll", () =>
                {
                    // REVIEW: Performance?
                    var ms = new MemoryStream();
                    stream.Position = 0;
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    return ms;

                }, isPublic: true));
            }

            sw.Stop();
            Trace.TraceInformation("Found {0} Assembly Neutral References in {1}ms", resources.Count, sw.ElapsedMilliseconds);
        }

        private HashSet<string> GetUsedReferences(Dictionary<string, Stream> assemblies)
        {
            var results = new HashSet<string>();

            // First we need to emit metadata reference for this compilation
            using (var metadataStream = new MemoryStream())
            {
                var result = Compilation.EmitMetadataOnly(metadataStream);

                if (!result.Success)
                {
                    // We failed to emit metadata so do nothing since we're probably
                    // going to fail compilation anyways
                    return results;
                }

                var stack = new Stack<Tuple<string, Stream>>();
                stack.Push(Tuple.Create((string)null, (Stream)metadataStream));

                while (stack.Count > 0)
                {
                    var top = stack.Pop();

                    var assemblyName = top.Item1;

                    if (!String.IsNullOrEmpty(assemblyName) &&
                        !results.Add(assemblyName))
                    {
                        // Skip the reference if saw it already
                        continue;
                    }

                    var stream = top.Item2;
                    stream.Position = 0;

                    foreach (var reference in GetReferences(stream))
                    {
                        Stream referenceStream;
                        if (assemblies.TryGetValue(reference, out referenceStream))
                        {
                            stack.Push(Tuple.Create(reference, referenceStream));
                        }
                    }
                }
            }

            return results;
        }

        private static IList<string> GetReferences(Stream stream)
        {
            var references = new List<string>();

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
}
