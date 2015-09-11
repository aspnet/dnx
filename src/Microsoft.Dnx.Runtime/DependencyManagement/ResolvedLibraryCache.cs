// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public class ResolvedLibraryCache : IResolvedLibraryCache
    {
        private ConcurrentDictionary<LibraryDescriptionKey, LibraryDescription> _cache = new ConcurrentDictionary<LibraryDescriptionKey, LibraryDescription>();

        public LibraryDescription GetOrAdd(LibraryDescription library)
        {
            Debug.Assert(library.Framework != null, "Framework must be set.");
            Debug.Assert(library.Identity != null, "Identity must be set.");
            Debug.Assert(library.RequestedRange != null, "RequestedRange must be set.");

            var key = new LibraryDescriptionKey(library);

            return _cache.GetOrAdd(key, library);
        }

        private struct LibraryDescriptionKey : IEquatable<LibraryDescriptionKey>
        {
            private readonly LibraryDescription _library;

            public LibraryDescriptionKey(LibraryDescription library)
            {
                _library = library;
            }

            public bool Equals(LibraryDescriptionKey other)
            {
                return
                    _library.Framework.Equals(other._library.Framework) &&
                    _library.Identity.Equals(other._library.Identity) &&
                    _library.RequestedRange.Equals(other._library.RequestedRange);
            }

            public override bool Equals(object obj)
            {
                var other = obj as LibraryDescriptionKey?;
                return other.HasValue ? Equals(other.Value) : false;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        _library.Framework.GetHashCode() * 17 +
                        _library.Identity.GetHashCode() * 13 +
                        _library.RequestedRange.GetHashCode() * 19;
                }
            }
        }
    }
}
