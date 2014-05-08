using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Web;

namespace Microsoft.Framework.PackageManager
{
    public class GraphNode
    {
        public GraphNode()
        {
            Dependencies = new List<GraphNode>();
        }

        public Library Library { get; set; }
        public List<GraphNode> Dependencies { get; private set; }
        public GraphItem Item { get; set; }
    }

}