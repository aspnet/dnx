// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilationContext : IBeforeCompileContext
    {
        private readonly Lazy<IList<ResourceDescription>> _resources;

        public Project Project { get; private set; }

        // Processed information
        public CSharpCompilation Compilation { get; set; }

        public IList<Diagnostic> Diagnostics { get; private set; }

        public IList<ResourceDescription> Resources { get { return _resources.Value; } }

        public IList<ICompileModule> Modules { get; } = new List<ICompileModule>();
        
        public IProjectContext ProjectContext { get; set; }

        public CompilationContext(CSharpCompilation compilation,
                                  Project project,
                                  FrameworkName targetFramework)
        {
            Compilation = compilation;
            Diagnostics = new List<Diagnostic>();
            Project = project;
            ProjectContext = new ProjectContext(project, targetFramework);
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

            return resources;
        }
    }
}
