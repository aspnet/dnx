// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Tooling.Algorithms;
using NuGet;

namespace Microsoft.Dnx.Tooling.List
{
    internal class MismatchedDependencyRenderer
    {
        private readonly DependencyListOptions _options;
        private readonly FrameworkName _framework;

        public MismatchedDependencyRenderer(DependencyListOptions options, FrameworkName framework)
        {
            _options = options;
            _framework = framework;
        }

        public void Render(IGraphNode<LibraryDependency> root)
        {
            // tuples of <Library Name, Requested Version, Actual Version>
            var results = new HashSet<Tuple<string, string, string>>();

            root.DepthFirstPreOrderWalk(
                (node, ancestors) =>
                {
                    var dependency = node.Item;
                    if (IsLibraryMismatch(dependency))
                    {
                        results.Add(Tuple.Create(
                            dependency.Library.Identity.Name,
                            dependency.LibraryRange.VersionRange?.MinVersion.ToString(),
                            dependency.Library.Identity.Version?.ToString()));
                    }

                    return true;
                });

            if (results.Any())
            {
                var format = GetFormat(results, padding: 2);

                RenderTitle(format);
                RenderMismatches(format, results);
            }
        }

        private void RenderTitle(string format)
        {
            _options.Reports.WriteInformation($"\n[Target framework {_framework} ({VersionUtility.GetShortFrameworkName(_framework)})]\n");
            _options.Reports.WriteInformation(string.Format(format, "Dependency", "Requested", "Resolved"));
            _options.Reports.WriteInformation(string.Format(format, "----------", "---------", "--------"));
        }

        private void RenderMismatches(string format, HashSet<Tuple<string, string, string>> results)
        {
            foreach (var row in results.OrderBy(r => r.Item1))
            {
                _options.Reports.WriteInformation(string.Format(format, row.Item1, row.Item2, row.Item3));
            }
        }

        private static string GetFormat(IEnumerable<Tuple<string, string, string>> data, int padding)
        {
            var columnWidths = new int[3];
            foreach (var row in data)
            {
                columnWidths[0] = Math.Max(row.Item1?.Length ?? 0, columnWidths[0]);
                columnWidths[1] = Math.Max(row.Item2?.Length ?? 0, columnWidths[1]);
                columnWidths[2] = Math.Max(row.Item3?.Length ?? 0, columnWidths[2]);
            }

            var builder = new StringBuilder();
            for (var idx = 0; idx < columnWidths.Length; ++idx)
            {
                builder.Append($"{{{idx}, -{columnWidths[idx] + padding}}}");
            }

            return builder.ToString();
        }

        private static bool IsLibraryMismatch(LibraryDependency dependency)
        {
            if (dependency.LibraryRange?.VersionRange != null)
            {
                // If we ended up with a declared version that isn't what was asked for directly
                // then report a warning
                // Case 1: Non floating version and the minimum doesn't match what was specified
                // Case 2: Floating version that fell outside of the range
                if ((dependency.LibraryRange.VersionRange.VersionFloatBehavior == SemanticVersionFloatBehavior.None &&
                     dependency.LibraryRange.VersionRange.MinVersion != dependency.Library.Identity.Version) ||
                    (dependency.LibraryRange.VersionRange.VersionFloatBehavior != SemanticVersionFloatBehavior.None &&
                     !dependency.LibraryRange.VersionRange.EqualsFloating(dependency.Library.Identity.Version)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
