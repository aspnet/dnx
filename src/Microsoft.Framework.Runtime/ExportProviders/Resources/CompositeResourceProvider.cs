// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Runtime.Compilation;

namespace Microsoft.Framework.Runtime
{
    public class CompositeResourceProvider : IResourceProvider
    {
        private static readonly Lazy<CompositeResourceProvider> _default = new Lazy<CompositeResourceProvider>(() =>
            new CompositeResourceProvider(new IResourceProvider[]
            {
                new EmbeddedResourceProvider(),
                new ResxResourceProvider()
            }));

        private readonly IEnumerable<IResourceProvider> _providers;

        public CompositeResourceProvider(IEnumerable<IResourceProvider> providers)
        {
            _providers = providers;
        }

        public IList<ResourceDescriptor> GetResources(ICompilationProject project)
        {
            return _providers
                .SelectMany(provider => provider.GetResources(project))
                .ToList();
        }

        public static CompositeResourceProvider Default
        {
            get
            {
                return _default.Value;
            }
        }
    }
}
