// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.Impl;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class DependencyContext
    {
        public DependencyContext(string projectDirectory, string configuration, FrameworkName targetFramework)
        {
            var applicationHostContext = new ApplicationHostContext(
                hostServices: null,
                projectDirectory: projectDirectory,
                packagesDirectory: null,
                configuration: configuration,
                targetFramework: targetFramework);
            DependencyWalker = applicationHostContext.DependencyWalker;
            FrameworkName = targetFramework;
            PackagesDirectory = applicationHostContext.PackagesDirectory;
        }

        private DependencyWalker DependencyWalker { get; }

        public LibraryManager LibraryManager { get; set; }
        public FrameworkName FrameworkName { get; set; }
        public string PackagesDirectory { get; private set; }

        public void Walk(string projectName, SemanticVersion projectVersion)
        {
            DependencyWalker.Walk(projectName, projectVersion, FrameworkName);
        }

        public static FrameworkName SelectFrameworkNameForRuntime(IEnumerable<FrameworkName> availableFrameworks, FrameworkName currentFramework, string runtime)
        {
            // Filter out frameworks incompatible with the current framework before selecting
            return SelectFrameworkNameForRuntime(
                availableFrameworks.Where(f => VersionUtility.IsCompatible(currentFramework, f)), 
                runtime);
        }

        public static FrameworkName SelectFrameworkNameForRuntime(IEnumerable<FrameworkName> availableFrameworks, string runtime)
        {
            var parts = runtime.Split(new[] { '.' }, 2);
            if (parts.Length != 2)
            {
                return null;
            }
            parts = parts[0].Split(new[] { '-' }, 4);
            if (parts.Length < 2)
            {
                return null;
            }
            if (parts.Length == 2 && !string.Equals(parts[1].ToLowerInvariant(), "mono"))
            {
                return null;
            }
            switch (parts[1].ToLowerInvariant())
            {
                case "mono":
                case "clr":
                    // CLR currently supports anything <= dnx46
                    return availableFrameworks
                        .Where(fx => fx.Identifier.Equals(FrameworkNames.LongNames.Dnx, StringComparison.Ordinal) && fx.Version <= new Version(4, 6))
                        .OrderByDescending(fx => fx.Version)
                        .FirstOrDefault();
                case "coreclr":
                    return availableFrameworks.FirstOrDefault(fx => fx.Equals(VersionUtility.ParseFrameworkName("dnxcore50")));
            }
            return null;
        }
    }
}
