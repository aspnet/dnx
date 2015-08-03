// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class AnalyzerSet
    {
        private readonly IEnumerable<IAnalyzerReference> _references;
        private readonly IAnalyzerAssemblyLoader _loader;
        private readonly FrameworkName _runtimeFramework;

        public AnalyzerSet(IAssemblyLoadContext loadContext,
                           IEnumerable<IAnalyzerReference> references,
                           FrameworkName runtimeFramework)
        {
            _loader = new AnalyzerAssemblyLoader(loadContext);
            _references = references;
            _runtimeFramework = runtimeFramework;

            DiagnosticAnalyzers = ImmutableArray<DiagnosticAnalyzer>.Empty;
        }

        public ImmutableArray<DiagnosticAnalyzer> DiagnosticAnalyzers { get; private set; }

        public void Load()
        {
            var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

            foreach (var reference in _references)
            {
                var files = reference.Files;

                foreach (var file in reference.Files)
                {
                    var fileRef = new AnalyzerFileReference(file, _loader);
                    builder.AddRange(fileRef.GetAnalyzers("C#"));
                }
            }

            DiagnosticAnalyzers = builder.ToImmutableArray();
        }

        private class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
        {
            private readonly IAssemblyLoadContext _loadContext;

            public AnalyzerAssemblyLoader(IAssemblyLoadContext loadContext)
            {
                _loadContext = loadContext;
            }

            public void AddDependencyLocation(string fullPath)
            {
                // do nothing
            }

            public Assembly LoadFromPath(string fullPath)
            {
                // Question: what if there are full paths have the same file name?
                return _loadContext.Load(Path.GetFileNameWithoutExtension(fullPath));
            }
        }
    }
}
