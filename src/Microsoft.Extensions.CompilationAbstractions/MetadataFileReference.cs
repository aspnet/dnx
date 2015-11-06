// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Extensions.CompilationAbstractions
{
    public class MetadataFileReference : IMetadataFileReference
    {
        public MetadataFileReference(string name, string path)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            Name = name;
            Path = path;
        }

        public string Name { get; }

        public string Path { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}