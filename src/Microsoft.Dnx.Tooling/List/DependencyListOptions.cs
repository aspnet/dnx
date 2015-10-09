// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.Dnx.Runtime.Helpers;

namespace Microsoft.Dnx.Tooling.List
{
    internal class DependencyListOptions
    {
        public DependencyListOptions(Reports reports, CommandArgument path)
        {
            bool isInputValid = true;

            // reports
            Reports = reports;

            // project
            var projectPath = path.Value ?? Directory.GetCurrentDirectory();
            Runtime.Project projectOption;

            isInputValid &= Runtime.Project.TryGetProject(projectPath, out projectOption);
            Path = projectPath;
            Project = projectOption;

            Valid = isInputValid;
        }

        public string Path { get; }

        public bool Valid { get; }

        public Runtime.Project Project { get; }

        public string RuntimeFolder { get; set; }

        public bool ShowAssemblies { get; set; }

        public IDictionary<FrameworkName, string> TargetFrameworks { get; private set; } = new Dictionary<FrameworkName, string>();

        public bool Details { get; set; }

        public bool Mismatch { get; set; }

        public string Single { get; set; }

        public string ResultsFilter { get; set; }

        public Reports Reports { get; }

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