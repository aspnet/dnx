// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Tooling.Packages
{
    /// <summary>
    /// Summary description for FeedOptions
    /// </summary>
    public class PackagesOptions
    {
        public PackagesOptions()
        {
        }

        public string SourcePackages { get; set; }

        public Reports Reports { get; set; }
    }
}
