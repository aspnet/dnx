// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime.Compilation
{
    internal class MetadataFileReference : IMetadataFileReference
    {
        public MetadataFileReference(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name
        {
            get;
            private set;
        }

        public string Path { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }
}