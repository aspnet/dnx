// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Framework.PackageManager.Algorithms;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager.List
{
    public class LibraryDependencyFlatRenderer
    {
        private readonly bool _showDetails;
        private readonly string _filterPattern;
        private readonly HashSet<string> _listedProjects;

        public LibraryDependencyFlatRenderer(bool showDetails, string filterPattern, IEnumerable<string> listedProjects)
        {
            _showDetails = showDetails;
            _filterPattern = filterPattern;
            _listedProjects = new HashSet<string>(listedProjects);
        }

        public IEnumerable<string> GetRenderContent(IGraphNode<LibraryDescription> root)
        {
            var dict = FindImmediateDependent(root);
            var libraries = dict.Keys.OrderBy(description => description.Identity.Name);
            var results = new List<string>();

            var gacOrFrameworkReferences = libraries.Where(library => library.Identity.IsGacOrFrameworkReference);

            if (gacOrFrameworkReferences.Any())
            {
                results.Add("Framework references:");
                RenderLibraries(gacOrFrameworkReferences, dict, results);
                results.Add(string.Empty);
            }

            var otherReferences = libraries.Where(library => !library.Identity.IsGacOrFrameworkReference);
            var referencesGroups = otherReferences.GroupBy(reference => reference.Type);
            foreach (var group in referencesGroups)
            {
                results.Add(string.Format("{0} references:", group.Key));
                RenderLibraries(group, dict, results);
                results.Add(string.Empty);
            }

            return results;
        }

        private IDictionary<LibraryDescription, ISet<LibraryDescription>> FindImmediateDependent(IGraphNode<LibraryDescription> root)
        {
            var result = new Dictionary<LibraryDescription, ISet<LibraryDescription>>();

            root.DepthFirstPreOrderWalk(
                visitNode: (node, ancestors) =>
                {
                    ISet<LibraryDescription> slot;
                    if (!result.TryGetValue(node.Item, out slot))
                    {
                        slot = new HashSet<LibraryDescription>();
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

        private void RenderLibraries(IEnumerable<LibraryDescription> descriptions,
                                     IDictionary<LibraryDescription, ISet<LibraryDescription>> dependenciesMap,
                                     IList<string> results)
        {
            if (!string.IsNullOrEmpty(_filterPattern))
            {
                var regex = new Regex("^" + Regex.Escape(_filterPattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase);
                descriptions = descriptions.Where(library => regex.IsMatch(library.Identity.Name));
            }

            foreach (var description in descriptions)
            {
                var libDisplay = (_listedProjects.Contains(description.Identity.Name) ? "* " : "  ") + description.Identity.ToString();

                if (description.Resolved)
                {
                    results.Add(libDisplay);
                }
                else
                {
                    results.Add(string.Format("{0} - Unresolved", libDisplay).Red().Bold());
                }

                if (_showDetails)
                {
                    var dependenciesInGroup = dependenciesMap[description].GroupBy(dep => dep.Type);
                    foreach (var group in dependenciesInGroup)
                    {
                        results.Add(string.Format("    by {0}: {1}", group.Key, string.Join(", ", group.Select(desc => desc.Identity.ToString()).OrderBy(name => name))));
                    }
                    results.Add(string.Empty);
                }
            }
        }
    }
}