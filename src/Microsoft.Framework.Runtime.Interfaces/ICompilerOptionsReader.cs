// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Specifies the contracts for a type that provides the <see cref="ICompilerOptions"/> for
    /// a <c>project.json</c> file.
    /// </summary>
    [AssemblyNeutral]
    public interface ICompilerOptionsReader
    {
        /// <summary>
        /// Returns the parsed <see cref="ICompilerOptions"/> for a project file specified by
        /// <paramref name="projectName"/>.
        /// </summary>
        /// <param name="projectName">Name or path of the project to read compilation settings from.</param>
        /// <returns>The parsed <see cref="ICompilerOptions"/>.</returns>
        ICompilerOptions ReadCompilerOptions(string json);

        ICompilerOptions ReadFrameworkCompilerOptions(string json, string shortName, FrameworkName targetFramework);

        ICompilerOptions ReadConfigurationCompilerOptions(string json, string configuration);
    }
}