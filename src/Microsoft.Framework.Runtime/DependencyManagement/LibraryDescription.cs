// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public class LibraryDescription
    {
        public Library Identity { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public FrameworkName Framework { get; set; }
        public IEnumerable<LibraryDependency> Dependencies { get; set; }
    }
}
