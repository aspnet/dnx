// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Dnx.Tooling.Algorithms
{
    public static class IGraphNodeExtensions
    {
        public static void DepthFirstPreOrderWalk<TNode>(
            this IGraphNode<TNode> root,
            Func<IGraphNode<TNode>, IEnumerable<IGraphNode<TNode>>, bool> visitNode)
        {
            DepthFirstGraphTraversal.PreOrderWalk(
                root: root,
                visitNode: visitNode,
                getChildren: node => node.Children);
        }
    }
}