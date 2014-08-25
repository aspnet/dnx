using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public static class EmbeddedReferencesHelper
    {
        private static readonly IList<IMetadataEmbeddedReference> _emptyList = new IMetadataEmbeddedReference[0];

        public static IList<IMetadataEmbeddedReference> GetRequiredEmbeddedReferences(CompilationContext context)
        {
            var assemblyNeutralTypes = context.MetadataReferences.OfType<IMetadataEmbeddedReference>()
                                              .ToDictionary(r => r.Name);

            // No assembly neutral types so do nothing
            if (assemblyNeutralTypes.Count == 0)
            {
                return _emptyList;
            }

            // Walk the assembly neutral references and embed anything that we use
            // directly or indirectly
            var results = GetUsedReferences(context, assemblyNeutralTypes);


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

        private static HashSet<string> GetUsedReferences(CompilationContext context, Dictionary<string, IMetadataEmbeddedReference> assemblies)
        {
            var results = new HashSet<string>();

            byte[] metadataBuffer = null;

            // First we need to emit just the metadata for this compilation
            using (var metadataStream = new MemoryStream())
            {
                var result = context.Compilation.Emit(metadataStream);

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

    }
}