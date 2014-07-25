// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    internal class MetadataFileReferenceFactory
    {
        // REVIEW: Should this be case sensitive since it contains file paths
        private readonly ConcurrentDictionary<string, MetadataReference> _metadataCache = new ConcurrentDictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        public MetadataReference GetMetadataReference(string path)
        {
            return _metadataCache.GetOrAdd(path, p =>
            {
                using (var stream = File.OpenRead(p))
                {
                    return new MetadataImageReference(stream);
                }
            });
        }
    }
}