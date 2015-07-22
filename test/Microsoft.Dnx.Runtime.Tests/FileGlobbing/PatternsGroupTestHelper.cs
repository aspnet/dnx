// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace Microsoft.Dnx.Runtime.Tests.FileGlobbing
{
    public static class PatternsGroupTestHelper
    {
        public static bool Equals(PatternGroup x, PatternGroup y)
        {
            if (!Enumerable.SequenceEqual(x.IncludePatterns.OrderBy(elem => elem),
                                          y.IncludePatterns.OrderBy(elem => elem)))
            {
                return false;
            }

            if (!Enumerable.SequenceEqual(x.ExcludePatterns.OrderBy(elem => elem),
                                          y.ExcludePatterns.OrderBy(elem => elem)))
            {
                return false;
            }

            if (!Enumerable.SequenceEqual(x.IncludeLiterals.OrderBy(elem => elem),
                                          y.IncludeLiterals.OrderBy(elem => elem)))
            {
                return false;
            }

            if (x.ExcludePatternsGroup.Count() != y.ExcludePatternsGroup.Count())
            {
                return false;
            }

            var xExcludeGroups = x.ExcludePatternsGroup.ToArray();
            var yExcludeGroups = x.ExcludePatternsGroup.ToArray();

            for (int idx = 0; idx < xExcludeGroups.Count(); ++idx)
            {
                if (!Equals(xExcludeGroups[idx], yExcludeGroups[idx]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}