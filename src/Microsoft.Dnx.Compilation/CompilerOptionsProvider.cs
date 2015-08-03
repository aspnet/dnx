// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Default implementation for <see cref="ICompilerOptionsProvider"/>.
    /// </summary>
    public class CompilerOptionsProvider : ICompilerOptionsProvider
    {
        private readonly IProjectResolver _projectResolver;

        public CompilerOptionsProvider(IProjectResolver projectResolver)
        {
            _projectResolver = projectResolver;
        }

        /// <inheritdoc />
        public ICompilerOptions GetCompilerOptions(string projectName, FrameworkName targetFramework, string configurationName)
        {
            Project project;
            if (_projectResolver.TryResolveProject(projectName, out project))
            {
                return project.GetCompilerOptions(targetFramework, configurationName);
            }

            return new CompilerOptions();
        }
    }
}