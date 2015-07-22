// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Compilation
{
    public class MetadataFileReference : IMetadataFileReference
    {
        public MetadataFileReference(string name, string path)
        {
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