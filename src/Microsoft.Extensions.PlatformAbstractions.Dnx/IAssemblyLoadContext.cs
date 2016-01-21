// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.Extensions.PlatformAbstractions
{
    /// <summary>
    /// A context in which assemblies can be loaded.
    /// </summary>
    public interface IAssemblyLoadContext : IDisposable
    {
        /// <summary>
        /// Load an assembly by name.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly.</param>
        /// <returns>The loaded assembly.</returns>
        Assembly Load(AssemblyName assemblyName);

        /// <summary>
        /// Loads the assembly located at the provided file system path.
        /// </summary>
        /// <param name="path">The fully qualified path of the file to load.</param>
        /// <returns>The loaded assembly.</returns>
        Assembly LoadFile(string path);

        /// <summary>
        /// Loads the assembly with a common object file format (COFF)-based image containing an emitted assembly, optionally including symbols for the assembly.
        /// </summary>
        /// <param name="assemblyStream">The stream representing the assembly.</param>
        /// <param name="assemblySymbols">The stream representing the symbols.</param>
        /// <returns>The loaded assembly.</returns>
        Assembly LoadStream(Stream assemblyStream, Stream assemblySymbols);

        /// <summary>
        /// Loads an unmanaged library by name.
        /// </summary>
        /// <param name="path">The name of the unmanaged library to load.</param>
        /// <returns>A handle to the unmanaged library or <c>IntPtr.Zero</c> if the library cannot be loaded.</returns>
        IntPtr LoadUnmanagedLibrary(string name);

        /// <summary>
        /// Loads an unmanaged library from the provided path.
        /// </summary>
        /// <param name="path">The path to load the unmanaged library from.</param>
        /// <returns>A handle to the unmanaged library or <c>IntPtr.Zero</c> if the library cannot be loaded.</returns>
        IntPtr LoadUnmanagedLibraryFromPath(string path);
    }
}
