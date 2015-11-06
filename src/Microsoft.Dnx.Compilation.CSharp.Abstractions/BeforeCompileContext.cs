// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class BeforeCompileContext
    {
        private Func<IList<ResourceDescriptor>> _lazyResources;
        private Func<IList<Diagnostic>> _lazyDiagnostics;
        private Func<IList<IMetadataReference>> _lazyMetadataReferences;

        private IList<ResourceDescriptor> _resources;
        private IList<Diagnostic> _diagnostics;
        private IList<IMetadataReference> _metadataReferences;

        public BeforeCompileContext()
        {
            _lazyResources = () => null;
            _lazyDiagnostics = () => null;
            _lazyMetadataReferences = () => null;
        }

        public BeforeCompileContext(
            CSharpCompilation compilation,
            ProjectContext projectContext,
            Func<IList<ResourceDescriptor>> lazyResources,
            Func<IList<Diagnostic>> lazyDiagnostics,
            Func<IList<IMetadataReference>> lazyMetadataReferences)
        {
            Compilation = compilation;
            ProjectContext = projectContext;
            _lazyResources = lazyResources;
            _lazyDiagnostics = lazyDiagnostics;
            _lazyMetadataReferences = lazyMetadataReferences;
        }

        public CSharpCompilation Compilation { get; set; }

        public ProjectContext ProjectContext { get; set; }

        public IList<ResourceDescriptor> Resources
        {
            get
            {
                return Init(ref _resources, ref _lazyResources);
            }
            set
            {
                _resources = value;
            }
        }

        public IList<Diagnostic> Diagnostics
        {
            get
            {
                return Init(ref _diagnostics, ref _lazyDiagnostics);
            }
            set
            {
                _diagnostics = value;
            }
        }

        public IList<IMetadataReference> MetadataReferences
        {
            get
            {
                return Init(ref _metadataReferences, ref _lazyMetadataReferences);
            }
            set
            {
                _metadataReferences = value;
            }
        }

        private T Init<T>(ref T list, ref Func<T> factory)
        {
            if (list == null && factory != null)
            {
                list = factory.Invoke();
                factory = null;
            }
            return list;
        }
    }
}
