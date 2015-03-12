using System;
using System.Reflection;
using Microsoft.Framework.Runtime.Dependencies;
using NuGet.ProjectModel;

namespace Microsoft.Framework.Runtime.Loader
{
    /// <summary>
    /// Loads .NET Assemblies from NuGet Packages
    /// </summary>
    /// <remarks>
    /// This loader REQUIRES that a lock file has been generated for the project.
    /// </remarks>
    public class PackageAssemblyLoader : IAssemblyLoader
    {
        private readonly LockFile _lockFile;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public PackageAssemblyLoader(IAssemblyLoadContextAccessor loadContextAccessor,
            LockFile lockFile)
        {
            _loadContextAccessor = loadContextAccessor;
            _lockFile = lockFile;
        }

        /// <summary>
        /// Loads an assembly from a NuGet package
        /// </summary>
        /// <remarks>
        /// Note that the Package name is never used when loading assemblies.
        /// If multiple packages provide the same assembly name, it is UNDEFINED
        /// which assembly will be loaded.
        /// </remarks>
        /// <param name="name">The name of the assembly to load</param>
        /// <returns>An <see cref="Assembly"/>, or null if the assembly could not be found</returns>
        public Assembly Load(string name)
        {
            return null;
        }
    }
}