// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Runtime;
using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager
{
    public class GraphItem
    {
        public WalkProviderMatch Match { get; set; }
        public IEnumerable<Library> Dependencies { get; set; }
    }
}
