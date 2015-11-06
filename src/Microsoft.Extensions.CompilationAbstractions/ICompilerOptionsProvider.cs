// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Extensions.CompilationAbstractions
{
    /// <summary>
    /// Specifies the contracts for a type that provides the <see cref="ICompilerOptions"/> for
    /// a <c>project.json</c> file.
    /// </summary>
    public interface ICompilerOptionsProvider
    {
        /// <summary>
        /// Returns the parsed <see cref="ICompilerOptions"/> for a project file specified by
        /// <paramref name="projectName"/>.
        /// </summary>
        /// <param name="projectName">Name or path of the project to read compilation settings from.</param>
        /// <param name="targetFramework">The <see cref="FrameworkName"/> to read framework-specific options from.
        /// When non-null, options for the specified framework are merged in to the result.</param>
        /// <param name="configurationName">The configuration to read configuration-specific options from.
        /// When non-null, options for the specified configuration are merged in to the result.</param>
        /// <returns>The parsed <see cref="ICompilerOptions"/>.</returns>
        ICompilerOptions GetCompilerOptions(string projectName, FrameworkName targetFramework, string configurationName);
    }
}