// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;

namespace Microsoft.Dnx.Runtime.Compilation
{
    /// <summary>
    /// Default implementation for <see cref="ICompilerOptionsProvider"/>.
    /// </summary>
    public class CompilerOptionsProvider : ICompilerOptionsProvider
    {
        private readonly LibraryManager _libraryManager;

        public CompilerOptionsProvider(LibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        /// <inheritdoc />
        public ICompilerOptions GetCompilerOptions(string projectName, FrameworkName targetFramework, string configurationName)
        {
            var projectDescription = _libraryManager.GetLibraryDescription(projectName) as ProjectDescription;

            if (projectDescription != null)
            {
                return projectDescription.Project.GetCompilerOptions(targetFramework, configurationName);
            }

            return new CompilerOptions();
        }
    }
}