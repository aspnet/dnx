// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;
using System;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Summary description for LibraryDescriptor
    /// </summary>
    public class LibraryDependency
    {
        public LibraryDependency(
            string name) : this(
                new Library
                {
                    Name = name
                },
                LibraryDependencyType.Default)
        {
        }

        public LibraryDependency(
            string name,
            bool isGacOrFrameworkReference) : this(
                new Library
                {
                    Name = name,
                    IsGacOrFrameworkReference = isGacOrFrameworkReference
                },
                LibraryDependencyType.Default)
        {
        }

        public LibraryDependency(
            string name,
            SemanticVersion version) : this(
                new Library
                {
                    Name = name,
                    Version = version
                },
                LibraryDependencyType.Default)
        {
        }

        public LibraryDependency(
            string name,
            SemanticVersion version,
            bool isGacOrFrameworkReference,
            LibraryDependencyType type) : this(
                new Library
                {
                    Name = name,
                    Version = version,
                    IsGacOrFrameworkReference = isGacOrFrameworkReference
                },
                type)
        {
        }

        public LibraryDependency(
            Library library) : this(
                library,
                LibraryDependencyType.Default)
        {
        }

        public LibraryDependency(
            Library library,
            LibraryDependencyType type)
        {
            Library = library;
            Type = type;
        }

        public Library Library { get; set; }

        public string Name
        {
            get { return Library.Name; }
        }

        public SemanticVersion Version
        {
            get { return Library.Version; }
        }

        public bool IsGacOrFrameworkReference
        {
            get { return Library.IsGacOrFrameworkReference; }
        }

        public LibraryDependencyType Type { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} {1}", Library, Type);
        }

        public LibraryDependency ChangeVersion(SemanticVersion version)
        {
            return new LibraryDependency(
                name: Library.Name,
                version: version,
                isGacOrFrameworkReference: Library.IsGacOrFrameworkReference,
                type: Type);
        }

        public bool HasFlag(LibraryDependencyTypeFlag flag)
        {
            return Type.Contains(flag);
        }
    }
}
