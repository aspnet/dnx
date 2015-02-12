// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Framework.PackageManager.Algorithms;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager.List
{
    public class LibraryDependencyFlatRenderer
    {
        private readonly bool _hideDependent;
        private readonly string _filterPattern;

        public LibraryDependencyFlatRenderer(bool hideDependent, string filterPattern)
        {
            _hideDependent = hideDependent;
            _filterPattern = filterPattern;
        }

        public IEnumerable<string> GetRenderContent(IGraphNode<Library> root)
        {
            var dict = FindImmediateDependent(root);
            var libraries = dict.Keys.OrderBy(library => library.Name);
            var results = new List<string>();

            RenderLibraries(libraries.Where(library => library.IsGacOrFrameworkReference), dict, results);
            RenderLibraries(libraries.Where(library => !library.IsGacOrFrameworkReference), dict, results);

            return results;
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

            // removing the root package
            result.Remove(root.Item);

            return result;
        }

        private void RenderLibraries(IEnumerable<Library> libraries, IDictionary<Library, ISet<Library>> dependenciesMap, List<string> results)
        {
            if (!string.IsNullOrEmpty(_filterPattern))
            {
                var regex = new Regex("^" + Regex.Escape(_filterPattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase);
                libraries = libraries.Where(library => regex.IsMatch(library.Name));
            }

            foreach (var lib in libraries)
            {
                if (!_hideDependent)
                {
                    results.Add(lib.ToString().Bold());

                    var dependents = string.Join(", ", dependenciesMap[lib].Select(dep => dep.ToString()).OrderBy(name => name));
                    results.Add(string.Format("    -> {0}", dependents));
                }
                else
                {
                    results.Add(lib.ToString());
                }
            }
        }
    }
}