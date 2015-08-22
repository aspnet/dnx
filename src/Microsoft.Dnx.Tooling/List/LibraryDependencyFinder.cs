// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.Internal;
using Microsoft.Dnx.Tooling.Algorithms;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling.List
{
    public class LibraryDependencyFinder
    {
        public static IGraphNode<LibraryDescription> Build([NotNull] IEnumerable<LibraryDescription> libraries, 
                                                           [NotNull]Runtime.Project project)
        {
            var libDictionary = libraries.ToDictionary(desc => desc.Identity);

            LibraryDescription root;
            if (!libDictionary.TryGetValue(new LibraryIdentity(project.Name, project.Version, isGacOrFrameworkReference: false), out root))
            {
                throw new InvalidOperationException(string.Format("Failed to retrieve {0} of project {1} - {2}", typeof(LibraryDependency).Name, project.Name, project.Version));
            }

            // build a tree of LibraryDescriptions of the given project root
            return DepthFirstGraphTraversal.PostOrderWalk<LibraryDescription, IGraphNode<LibraryDescription>>(
                node: root,
                getChildren: node =>
                {
                    if (node.Resolved)
                    {
                        return node.Dependencies.Select(dependency => libDictionary[dependency.Library.Identity]);
                    }
                    else
                    {
                        return Enumerable.Empty<LibraryDescription>();
                    }
                },
                visitNode: (node, children) =>
                {
                    return new GraphNode<LibraryDescription>(node, children);
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
