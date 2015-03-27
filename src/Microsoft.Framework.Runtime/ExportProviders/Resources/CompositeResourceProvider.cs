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
                new OldEmbeddedResourceProvider(),
                new EmbeddedResourceProvider(),
                new OldResxResourceProvider(),
                new ResxResourceProvider()
            }));

        private readonly IEnumerable<IResourceProvider> _providers;

        public CompositeResourceProvider(IEnumerable<IResourceProvider> providers)
        {
            _providers = providers;
        }

        public IList<ResourceDescriptor> GetResources(ICompilationProject project)
        {
            // Keep only the distinct names to prevent compilation errors (TEMPORARY BOOTSTRAP WORKAROUND)
            return _providers
                .SelectMany(provider => provider.GetResources(project))
                .GroupBy(res => res.Name)
                .Select(g => g.First())
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
