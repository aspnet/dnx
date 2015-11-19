// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class LockFileProjectLibrary
    {
        public string Name { get; set; }

        public SemanticVersion Version { get; set; }

        public string Path { get; set; }
    }
}