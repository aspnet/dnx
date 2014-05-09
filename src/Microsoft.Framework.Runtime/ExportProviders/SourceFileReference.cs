// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace Microsoft.Framework.Runtime
{
    public class SourceFileReference : ISourceFileReference
    {
        public SourceFileReference(string path)
        {
            Path = path;
        }

        public string Path { get; private set; }
    }
}