// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Compilation.CSharp
{
    /// <summary>
    /// Extension methods for <see cref="IMetadataReference"/>
    /// </summary>
    public static class MetadataReferenceExtensions
    {
        /// <summary>
        /// Converts an <see cref="IMetadataReference"/> to a <see cref="MetadataReference"/> instance.
        /// </summary>
        /// <param name="metadataReference">The <see cref="IMetadataReference"/> to convert.</param>
        /// <param name="assemblyMetadataFactory">Factory invoked to get instances of <see cref="AssemblyMetadata"/>.</param>
        /// <returns>The converted <see cref="MetadataReference"/>.</returns>
        public static MetadataReference ConvertMetadataReference(
            this IMetadataReference metadataReference,
            Func<IMetadataFileReference, AssemblyMetadata> assemblyMetadataFactory)
        {
            var roslynReference = metadataReference as IRoslynMetadataReference;

            if (roslynReference != null)
            {
                return roslynReference.MetadataReference;
            }

            var embeddedReference = metadataReference as IMetadataEmbeddedReference;

            if (embeddedReference != null)
            {
                return MetadataReference.CreateFromImage(embeddedReference.Contents);
            }

            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null)
            {
                var metadata = assemblyMetadataFactory(fileMetadataReference);
                return metadata.GetReference(filePath: fileMetadataReference.Path);
            }

            var projectReference = metadataReference as IMetadataProjectReference;
            if (projectReference != null)
            {
                using (var ms = new MemoryStream())
                {
                    projectReference.EmitReferenceAssembly(ms);

                    return MetadataReference.CreateFromStream(ms, filePath: projectReference.ProjectPath);
                }
            }

            throw new NotSupportedException($"Unsupported type '{metadataReference.GetType()}'.");
        }

        /// <summary>
        /// Creates a <see cref="AssemblyMetadata"/> for the assembly specified by <paramref name="fileReference"/>.
        /// </summary>
        /// <param name="fileReference">The <see cref="IMetadataFileReference"/>.</param>
        /// <returns>An <see cref="AssemblyMetadata"/>.</returns>
        public static AssemblyMetadata CreateAssemblyMetadata(this IMetadataFileReference fileReference)
        {
            using (var stream = File.OpenRead(fileReference.Path))
            {
                var moduleMetadata = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata);
                return AssemblyMetadata.Create(moduleMetadata);
            }
        }
    }
}
