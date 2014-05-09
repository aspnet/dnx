// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.Runtime
{
    public sealed class LibraryInformation : ILibraryInformation
    {
        public LibraryInformation(LibraryDescription description)
        {
            Name = description.Identity.Name;
            Path = description.Path;
            Type = description.Type;
            Dependencies = description.Dependencies.Select(d => d.Name);
        }

        public LibraryInformation(string name, IEnumerable<string> dependencies)
        {
            Name = name;
            Dependencies = dependencies;
        }

        public string Name
        {
            get;
            private set;
        }

        public string Path
        {
            get;
            private set;
        }

        public string Type
        {
            get;
            private set;
        }

        public IEnumerable<string> Dependencies
        {
            get;
            private set;
        }
    }
}
