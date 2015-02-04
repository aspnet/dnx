// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilationContext : IBeforeCompileContext
    {
        private readonly Lazy<IList<ResourceDescription>> _resources;

        /// <summary>
        /// The project associated with this compilation
        /// </summary>
        public Project Project { get; private set; }

        // Processed information
        public CSharpCompilation Compilation { get; private set; }

        public IList<Diagnostic> Diagnostics { get; private set; }

        public IList<IMetadataReference> MetadataReferences { get; private set; }

        public IList<ResourceDescription> Resources { get { return _resources.Value; } }

        CSharpCompilation IBeforeCompileContext.CSharpCompilation
        {
            get
            {
                return Compilation;
            }

            set
            {
                Compilation = value;
            }
        }

        public CompilationContext(CSharpCompilation compilation,
                                  IList<IMetadataReference> metadataReferences,
                                  IList<Diagnostic> diagnostics,
                                  Project project)
        {
            Compilation = compilation;
            MetadataReferences = metadataReferences;
            Diagnostics = diagnostics;
            Project = project;
            _resources = new Lazy<IList<ResourceDescription>>(() => GetResources(this));
        }

        private static IList<ResourceDescription> GetResources(CompilationContext context)
        {
            var resxProvider = new ResxResourceProvider();
            var embeddedResourceProvider = new EmbeddedResourceProvider();

            var resourceProvider = new CompositeResourceProvider(new IResourceProvider[] { resxProvider, embeddedResourceProvider });

            var sw = Stopwatch.StartNew();
            Logger.TraceInformation("[{0}]: Generating resources for {1}", nameof(CompilationContext), context.Project.Name);

            var resources = resourceProvider.GetResources(context.Project);

            sw.Stop();
            Logger.TraceInformation("[{0}]: Generated resources for {1} in {2}ms", nameof(CompilationContext), context.Project.Name, sw.ElapsedMilliseconds);

            sw = Stopwatch.StartNew();
            Logger.TraceInformation("[{0}]: Resolving required assembly neutral references for {1}", nameof(CompilationContext), context.Project.Name);

            var embeddedReferences = EmbeddedReferencesHelper.GetRequiredEmbeddedReferences(context);
            resources.AddEmbeddedReferences(embeddedReferences);

            Logger.TraceInformation("[{0}]: Resolved {1} required assembly neutral references for {2} in {3}ms",
                nameof(CompilationContext),
                embeddedReferences.Count,
                context.Project.Name,
                sw.ElapsedMilliseconds);
            sw.Stop();

            return resources;
        }
    }
}
