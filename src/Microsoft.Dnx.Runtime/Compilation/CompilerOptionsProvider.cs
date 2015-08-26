// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime.Compilation
{
    /// <summary>
    /// Default implementation for <see cref="ICompilerOptionsProvider"/>.
    /// </summary>
    public class CompilerOptionsProvider : ICompilerOptionsProvider
    {
        private readonly IDictionary<string, ProjectDescription> _projects;

        public CompilerOptionsProvider(IDictionary<string, ProjectDescription> projects)
        {
            _projects = projects;
        }

        /// <inheritdoc />
        public ICompilerOptions GetCompilerOptions(string projectName, FrameworkName targetFramework, string configurationName)
        {
            ProjectDescription projectDescription;
            if (_projects.TryGetValue(projectName, out projectDescription))
            {
                return projectDescription.Project.GetCompilerOptions(targetFramework, configurationName);
            }

            return new CompilerOptions();
        }
    }
}