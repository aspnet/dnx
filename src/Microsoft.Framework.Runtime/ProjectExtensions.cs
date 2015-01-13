// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public static class ProjectExtensions
    {
        public static ICompilerOptions GetCompilerOptions(this Project project,
                                                         FrameworkName targetFramework,
                                                         string configurationName)
        {
            // Get all project options and combine them
            var options = project.GetCompilerOptions();
            if (configurationName != null)
            {
                var configurationOptions = project.GetCompilerOptions(configurationName);
                if (configurationOptions != null)
                {
                    options = options.Merge(configurationOptions);
                }
            }

            if (targetFramework != null)
            {
                var targetFrameworkOptions = project.GetCompilerOptions(targetFramework);
                if (targetFrameworkOptions != null)
                {
                    options = options.Merge(targetFrameworkOptions);
                }
            }

            return options;
        }
    }
}