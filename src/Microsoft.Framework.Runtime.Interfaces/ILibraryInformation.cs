// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Exposes information about a library which can be an assembly, project, or a package.
    /// </summary>
    public interface ILibraryInformation
    {
        /// <summary>
        /// Gets the name of the library.
        /// </summary>
        string Name { get; }

        /// <summary>
        ///  Gets the version of the library.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the path to the library. For projects, this is a project.json file.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Gets the type of library. Common values are Project, Package, and Assembly.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Gets the list of dependencies for the library.
        /// </summary>
        IEnumerable<string> Dependencies { get; }

        /// <summary>
        /// Gets the list of assembly names from the library that can be loaded. Packages can contain multiple assemblies.
        /// </summary>
        IEnumerable<AssemblyName> LoadableAssemblies { get; }
    }
}