// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Framework.PackageManager.Algorithms;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager.List
{
    public class LibraryDependencyFlatRenderer
    {
        private IReport _output;

        public LibraryDependencyFlatRenderer(IReport output)
        {
            _output = output;
        }

        public void Render(IGraphNode<Library> root)
        {
            var dict = FindImmediateDependent(root);
            foreach (var key in dict.Keys.OrderBy(library => library.Name))
            {
                _output.WriteLine(key.ToString().White().Bold());
                _output.WriteLine("    -> {0}", string.Join(", ", dict[key].Select(lib => lib.ToString()).OrderBy(s => s)));
            }
        }

        private IDictionary<Library, ISet<Library>> FindImmediateDependent(IGraphNode<Library> root)
        {
            var result = new Dictionary<Library, ISet<Library>>();

            root.DepthFirstPreOrderWalk(
                visitNode: (node, ancestors) =>
                {
                    ISet<Library> slot;
                    if (!result.TryGetValue(node.Item, out slot))
                    {
                        slot = new HashSet<Library>();
                        result.Add(node.Item, slot);
                    }

                    // first item in the path is the immediate parent
                    if (ancestors.Any())
                    {
                        slot.Add(ancestors.First().Item);
                    }

                    return true;
                });

            return result;
        }
    }
}