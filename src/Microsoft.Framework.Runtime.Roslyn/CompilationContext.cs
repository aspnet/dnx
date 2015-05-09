// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilationContext
    {
        private readonly BeforeCompileContext _beforeCompileContext;
        private readonly Func<IList<ResourceDescriptor>> _resourcesResolver;

        public IList<IMetadataReference> References { get; set; }

        public CompilationContext(CSharpCompilation compilation,
                                  ICompilationProject project,
                                  FrameworkName targetFramework,
                                  string configuration,
                                  IEnumerable<IMetadataReference> incomingReferences,
                                  Func<IList<ResourceDescriptor>> resourcesResolver)
        {
            Project = project;
            Modules = new List<ICompileModule>();

            _resourcesResolver = resourcesResolver;

            var projectContext = new ProjectContext
            {
                Name = project.Name,
                ProjectDirectory = project.ProjectDirectory,
                ProjectFilePath = project.ProjectFilePath,
                TargetFramework = targetFramework,
                Version = project.Version?.ToString(),
                Configuration = configuration
            };

            _beforeCompileContext = new BeforeCompileContext(
                compilation,
                projectContext,
                ResolveResources,
                () => new List<Diagnostic>(),
                () => new List<IMetadataReference>(incomingReferences)
            );
        }

        public ICompilationProject Project { get; }

        public IList<ICompileModule> Modules { get; }

        public CSharpCompilation Compilation
        {
            get { return _beforeCompileContext.Compilation; }
            set { _beforeCompileContext.Compilation = value; }
        }

        public IList<Diagnostic> Diagnostics
        {
            get { return _beforeCompileContext.Diagnostics; }
        }

        public IList<ResourceDescriptor> Resources
        {
            get { return _beforeCompileContext.Resources; }
        }

        public ProjectContext ProjectContext
        {
            get { return _beforeCompileContext.ProjectContext; }
        }

        public BeforeCompileContext BeforeCompileContext
        {
            get { return _beforeCompileContext; }
        }

        private IList<ResourceDescriptor> ResolveResources()
        {
            var sw = Stopwatch.StartNew();
            Logger.TraceInformation("[{0}]: Generating resources for {1}", nameof(CompilationContext), Project.Name);

            var resources = _resourcesResolver();

            sw.Stop();
            Logger.TraceInformation("[{0}]: Generated resources for {1} in {2}ms", nameof(CompilationContext),
                                                                                   Project.Name,
                                                                                   sw.ElapsedMilliseconds);

            return resources;
        }
    }
}
