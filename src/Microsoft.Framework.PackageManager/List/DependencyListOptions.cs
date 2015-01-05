// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet;

namespace Microsoft.Framework.PackageManager.List
{
    internal class DependencyListOptions
    {
        public DependencyListOptions(Reports reports, CommandArgument path, CommandOption framework)
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

            // framework
            if (framework.HasValue())
            {
                try
                {
                    Framework = VersionUtility.ParseFrameworkName(framework.Value());
                }
                catch (ArgumentException ex)
                {
                    Reports.Error.WriteLine("Invalid framework name: {0}. [{1}]", framework.Value(), ex.Message);
                    isInputValid = false;
                }
            }
            else
            {
                Framework = null;
            }

            Valid = isInputValid;
        }

        public string Path { get; }

        public bool Valid { get; }

        public Runtime.Project Project { get; }

        public string RuntimeFolder { get; set; }

        public bool ShowAssemblies { get; set; }

        public FrameworkName Framework { get; }

        public Reports Reports { get; }
    }
}