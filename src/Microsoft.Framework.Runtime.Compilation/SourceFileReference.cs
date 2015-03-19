// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace Microsoft.Framework.Runtime.Compilation
{
    public class SourceFileReference : ISourceFileReference
    {
        public SourceFileReference(string path)
        {
            // Unique name of the reference
            Name = path;
            Path = path;
        }

        public string Name { get; private set; }

        public string Path { get; private set; }

        public override string ToString()
        {
            return "Source: " + Path;
        }
    }
}