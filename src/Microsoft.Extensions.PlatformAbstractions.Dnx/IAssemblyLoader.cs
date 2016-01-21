// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace Microsoft.Extensions.PlatformAbstractions
{
    /// <summary>
    /// Defines a contract for an assembly loader. This is an extension point that can be used to implement custom assembly loading logic.
    /// </summary>
    public interface IAssemblyLoader
    {
        /// <summary>
        /// Load an assembly by name.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly.</param>
        /// <returns>The loaded assembly.</returns>
        Assembly Load(AssemblyName assemblyName);

        /// <summary>
        /// Loads an unmanaged library by name.
        /// </summary>
        /// <param name="name">The name of the library to load.</param>
        /// <returns>A handle to the unmanaged library or <c>IntPtr.Zero</c> if the library cannot be loaded.</returns>
        IntPtr LoadUnmanagedLibrary(string name);
    }
}
