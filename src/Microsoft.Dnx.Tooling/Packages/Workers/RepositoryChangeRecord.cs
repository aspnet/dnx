// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Dnx.Tooling.Packages.Workers
{
    public class RepositoryChangeRecord
    {
        public int Next { get; set; }

        public IEnumerable<string> Add { get; set; }

        public IEnumerable<string> Remove { get; set; }
    }
}