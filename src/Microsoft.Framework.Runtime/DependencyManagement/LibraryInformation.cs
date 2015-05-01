// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    public sealed class LibraryInformation : ILibraryInformation
    {
        public LibraryInformation(LibraryDescription description)
        {
            Name = description.Identity.Name;
            Version = description.Identity.Version?.ToString();
            Path = description.Path;
            Type = description.Type;
            Dependencies = description.Dependencies.Select(d => d.Name);
            LoadableAssemblies = description.LoadableAssemblies.Select(a => new AssemblyName(a));
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

        public string Version
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

        public IEnumerable<AssemblyName> LoadableAssemblies
        {
            get;
            private set;
        }
    }
}
