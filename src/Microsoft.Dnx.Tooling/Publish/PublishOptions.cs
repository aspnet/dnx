// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Helpers;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishOptions
    {
        public string OutputDir { get; set; }

        public string ProjectDir { get; set; }

        public string Configuration { get; set; }

        public string WwwRoot { get; set; }

        public string WwwRootOut { get; set; }

        public FrameworkName RuntimeActiveFramework { get; set; }

        public bool NoSource { get; set; }

        public IList<string> Runtimes { get; set; }

        public IDictionary<FrameworkName, string> TargetFrameworks { get; private set; } = new Dictionary<FrameworkName, string>();

        public bool Native { get; set; }

        public bool IncludeSymbols { get; set; }

        public Reports Reports { get; set; }

        public string IISCommand { get; set; }

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