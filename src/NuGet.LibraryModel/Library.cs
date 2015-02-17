// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.LibraryModel
{
    public class Library
    {
        public LibraryRange RequestedRange { get; set; }
        public LibraryIdentity Identity { get; set; }
        public IEnumerable<LibraryDependency> Dependencies { get; set; }
        public bool Resolved { get; set; } = true;
        public string Path { get; set; }
        public string Type { get; set; }

        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

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
