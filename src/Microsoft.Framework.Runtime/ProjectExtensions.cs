// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public static class ProjectExtensions
    {
        public static CompilerOptions GetCompilerOptions(this Project project,
                                                         FrameworkName targetFramework,
                                                         string configurationName)
        {
            // Get all project options and combine them
            var rootOptions = project.GetCompilerOptions();
            var configurationOptions = configurationName != null ? project.GetCompilerOptions(configurationName) : null;
            var targetFrameworkOptions = targetFramework != null ? project.GetCompilerOptions(targetFramework) : null;

            // Combine all of the options
            return CompilerOptions.Combine(rootOptions, configurationOptions, targetFrameworkOptions);
        }
    }
}