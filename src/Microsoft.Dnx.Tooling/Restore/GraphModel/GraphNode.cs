// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Runtime;
using System.Collections.Generic;

namespace Microsoft.Dnx.Tooling
{
    public class GraphNode
    {
        public GraphNode()
        {
            Dependencies = new List<GraphNode>();
            Disposition = DispositionType.Acceptable;
        }

        public LibraryRange LibraryRange { get; set; }
        public List<GraphNode> Dependencies { get; private set; }
        public GraphItem Item { get; set; }
        public DispositionType Disposition { get; set; }

        public enum DispositionType
        {
            Acceptable,
            Rejected,
            Accepted
        }
    }

}