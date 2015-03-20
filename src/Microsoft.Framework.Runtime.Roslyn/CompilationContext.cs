// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilationContext : IBeforeCompileContext
    {
        private readonly Lazy<IList<ResourceDescription>> _resources;

        public ICompilationProject Project { get; private set; }

        // Processed information
        public CSharpCompilation Compilation { get; set; }

        public IList<Diagnostic> Diagnostics { get; private set; }

        public IList<ResourceDescription> Resources { get { return _resources.Value; } }

        public IList<ICompileModule> Modules { get; } = new List<ICompileModule>();

        public IProjectContext ProjectContext { get; set; }

        public CompilationContext(CSharpCompilation compilation,
                                  ICompilationProject project,
                                  FrameworkName targetFramework,
                                  string configuration,
                                  Func<IList<ResourceDescription>> resourcesResolver)
        {
            Compilation = compilation;
            Diagnostics = new List<Diagnostic>();
            Project = project;
            ProjectContext = new ProjectContext(project, targetFramework, configuration);
            _resources = new Lazy<IList<ResourceDescription>>(() =>
            {
                var sw = Stopwatch.StartNew();
                Logger.TraceInformation("[{0}]: Generating resources for {1}", nameof(CompilationContext), Project.Name);

                var resources = resourcesResolver();

                sw.Stop();
                Logger.TraceInformation("[{0}]: Generated resources for {1} in {2}ms", nameof(CompilationContext), Project.Name, sw.ElapsedMilliseconds);

                return resources;
            });
        }
    }
}
