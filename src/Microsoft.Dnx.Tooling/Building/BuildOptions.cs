// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Helpers;

namespace Microsoft.Dnx.Tooling
{
    public class BuildOptions
    {
        public string OutputDir { get; set; }

        public IList<string> ProjectPatterns { get; set; }

        public IList<string> Configurations { get; set; }

        public IDictionary<FrameworkName, string> TargetFrameworks { get; private set; } = new Dictionary<FrameworkName, string>();

        public bool GeneratePackages { get; set; }

        public Reports Reports { get; set; }

        public BuildOptions()
        {
            Configurations = new List<string>();
            ProjectPatterns = new List<string>();
        }

        public void AddFrameworkMonikers(IEnumerable<string> monikers)
        {
            if (monikers != null)
            {
                foreach (var moniker in monikers.Distinct())
                {
                    TargetFrameworks.Add(FrameworkNameHelper.ParseFrameworkName(moniker), moniker);
                }
            }
        }
    }
}
