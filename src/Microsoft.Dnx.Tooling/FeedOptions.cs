// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Dnx.Tooling
{
    public class FeedOptions
    {
        public IList<string> FallbackSources { get; set; } = new List<string>();

        public bool IgnoreFailedSources { get; set; }

        public bool NoCache { get; set; }

        public string TargetPackagesFolder { get; set; }

        public string Proxy { get; set; }

        public IList<string> Sources { get; set; } = new List<string>();

        public bool Quiet { get; set; }

        /// <summary>
        /// Gets or sets a flag that determines if restore is performed on multiple project.json files in parallel.
        /// </summary>
        public bool Parallel { get; set; }
    }
}
