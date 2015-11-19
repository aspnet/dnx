// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.Dnx.Tooling.Algorithms
{
    public static class DepthFirstGraphTraversal
    {
        public static void PreOrderWalk<TNode>(
            TNode root,
            Func<TNode, IEnumerable<TNode>, bool> visitNode,
            Func<TNode, IEnumerable<TNode>> getChildren)
        {
            if (visitNode == null)
            {
                throw new ArgumentNullException(nameof(visitNode));
            }

            if (getChildren == null)
            {
                throw new ArgumentNullException(nameof(getChildren));
            }

            PreOrderWalk(root, new Stack<TNode>(), visitNode, getChildren);
        }

        public static TResult PostOrderWalk<TNode, TResult>(
            TNode node,
            Func<TNode, IEnumerable<TResult>, TResult> visitNode,
            Func<TNode, IEnumerable<TNode>> getChildren)
        {
            var children = getChildren(node);

            IEnumerable<TResult> childResults;
            if (children != null && children.Any())
            {
                childResults = children.Select(child => PostOrderWalk(child, visitNode, getChildren)).ToArray();
            }
            else
            {
                childResults = Enumerable.Empty<TResult>();
            }

            return visitNode(node, childResults);
        }

        private static void PreOrderWalk<TNode>(
            TNode current,
            Stack<TNode> ancestors,
            Func<TNode, IEnumerable<TNode>, bool> visitNode,
            Func<TNode, IEnumerable<TNode>> getChildren)
        {
            if (visitNode(current, ancestors))
            {
                ancestors.Push(current);

                var children = getChildren(current) ?? Enumerable.Empty<TNode>();
                foreach (var child in children)
                {
                    PreOrderWalk(child, ancestors, visitNode, getChildren);
                }

                ancestors.Pop();
            }
        }
    }
}