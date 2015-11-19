// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Dnx.Tooling.Packages.Workers
{
    public class RepositoryTransmitRecord
    {
        public IDictionary<string, int> Push { get; set; }

        public IDictionary<string, int> Pull { get; set; }
    }
}