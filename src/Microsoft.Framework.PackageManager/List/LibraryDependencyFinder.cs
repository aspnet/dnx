// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.PackageManager.Algorithms;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager.List
{
    public class LibraryDependencyFinder
    {
        private readonly IDictionary<Library, LibraryDescription> _libDictionary;

        public LibraryDependencyFinder(IDictionary<Library, LibraryDescription> libDictionary)
        {
            _libDictionary = libDictionary;
        }

        public IGraphNode<Library> Build(Library root)
        {
            return DepthFirstGraphTraversal.PostOrderWalk<Library, IGraphNode<Library>>(
                node: root,
                getChildren: node =>
                {
                    LibraryDescription desc;
                    if (_libDictionary.TryGetValue(node, out desc))
                    {
                        return desc.Dependencies.Select(dep => dep.Library);
                    }
                    else
                    {
                        return Enumerable.Empty<Library>();
                    }
                },
                visitNode: (node, children) =>
                {
                    return new GraphNode<Library>(node, children);
                });
        }

        private class GraphNode<TValue> : IGraphNode<TValue>
        {
            public GraphNode(TValue item, IEnumerable<IGraphNode<TValue>> children = null)
            {
                Item = item;
                Children = children ?? Enumerable.Empty<IGraphNode<TValue>>();
            }

            public IEnumerable<IGraphNode<TValue>> Children { get; }

            public TValue Item { get; }
        }
    }
}