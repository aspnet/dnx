using System;
using System.IO;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// A context in which assemblies can be loaded.
    /// </summary>
    public interface IAssemblyLoadContext : IDisposable
    {
        /// <summary>
        /// Load an assembly by name.
        /// </summary>
        /// <param name="name">The name of the assembly.</param>
        /// <returns>The loaded assembly.</returns>
        Assembly Load(string name);

        /// <summary>
        /// Loads the contents of an assembly file on the specified path. 
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
    }
}