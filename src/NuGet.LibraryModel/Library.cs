// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.LibraryModel
{
    public class Library
    {
        public LibraryRange RequestedRange { get; }
        public LibraryIdentity Identity { get; }
        public IEnumerable<LibraryDependency> Dependencies { get; }
        public bool Resolved { get; } = true;
        public string Path { get; }

        /// <summary>
        /// A set of arbitrary properties useful for resolving dependencies on this library and loading the library
        /// </summary>
        public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        public Library(
            LibraryRange requestedRange,
            LibraryIdentity identity,
            IEnumerable<LibraryDependency> dependencies,
            string path)
        {
            RequestedRange = requestedRange;
            Identity = identity;
            Dependencies = dependencies;
            Path = path;
        }

        public override string ToString()
        {
            if (Identity == null)
            {
                return RequestedRange?.ToString();
            }

            return Identity + " (" + RequestedRange + ")";
        }
    }
}
