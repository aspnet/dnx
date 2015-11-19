// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Tooling.Algorithms;

namespace Microsoft.Dnx.Tooling.List
{
    public class LibraryDependencyFinder
    {
        public static IGraphNode<LibraryDependency> Build(
            IEnumerable<LibraryDescription> libraries,
            Runtime.Project project)
        {
            if (libraries == null)
            {
                throw new ArgumentNullException(nameof(libraries));
            }

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            var root = new LibraryDependency
            {
                Library = libraries.FirstOrDefault(p => string.Equals(p.Identity.Name, project.Name)),
                LibraryRange = null
            };

            if (root.Library == null)
            {
                throw new InvalidOperationException($"Failed to retrieve {nameof(LibraryDependency)} of project {project.Name}/{project.Version}");
            }

            // build a tree of LibraryDescriptions of the given project root
            return DepthFirstGraphTraversal.PostOrderWalk<LibraryDependency, IGraphNode<LibraryDependency>>(
                node: root,
                getChildren: node =>
                {
                    if (node.Library.Resolved)
                    {
                        return node.Library.Dependencies;
                    }
                    else
                    {
                        return Enumerable.Empty<LibraryDependency>();
                    }
                },
                visitNode: (node, children) =>
                {
                    return new GraphNode<LibraryDependency>(node, children);
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
