// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dnx.Testing
{
    public class DirDiff
    {
        public IEnumerable<string> MissingEntries { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> ExtraEntries { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> DifferentEntries { get; set; } = Enumerable.Empty<string>();

        public bool NoDiff
        {
            get
            {
                return !MissingEntries.Any() && !ExtraEntries.Any() && !DifferentEntries.Any();
            }
        }
    }
}
