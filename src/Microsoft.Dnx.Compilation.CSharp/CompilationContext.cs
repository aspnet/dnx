// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class CompilationContext
    {
        private readonly BeforeCompileContext _beforeCompileContext;
        private readonly Func<IList<ResourceDescriptor>> _resourcesResolver;

        public IList<IMetadataReference> References { get; set; }

        public CompilationContext(CSharpCompilation compilation,
                                  CompilationProjectContext compilationContext,
                                  IEnumerable<IMetadataReference> incomingReferences,
                                  Func<IList<ResourceDescriptor>> resourcesResolver)
        {
            Project = compilationContext;
            Modules = new List<ICompileModule>();

            _resourcesResolver = resourcesResolver;

            var projectContext = new ProjectContext
            {
                Name = compilationContext.Target.Name,
                ProjectDirectory = compilationContext.ProjectDirectory,
                ProjectFilePath = compilationContext.ProjectFilePath,
                TargetFramework = compilationContext.Target.TargetFramework,
                Version = compilationContext.Version?.ToString(),
                Configuration = compilationContext.Target.Configuration
            };

            _beforeCompileContext = new BeforeCompileContext(
                compilation,
                projectContext,
                ResolveResources,
                () => new List<Diagnostic>(),
                () => new List<IMetadataReference>(incomingReferences)
             );
        }

        public CompilationProjectContext Project { get; }

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
            Logger.TraceInformation("[{0}]: Generating resources for {1}", nameof(CompilationContext), Project.Target.Name);

            var resources = _resourcesResolver();

            sw.Stop();
            Logger.TraceInformation("[{0}]: Generated resources for {1} in {2}ms", nameof(CompilationContext),
                                                                                   Project.Target.Name,
                                                                                   sw.ElapsedMilliseconds);

            return resources;
        }
    }
}
