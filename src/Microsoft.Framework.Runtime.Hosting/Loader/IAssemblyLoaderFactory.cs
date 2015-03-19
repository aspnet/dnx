using System;
using System.Collections.Generic;
using Microsoft.Framework.Runtime.Dependencies;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Provides an interface to a factory that can create an
    /// <see cref="IAssemblyLoader"/> based on a set of resolved
    /// <see cref="Library"/> dependendencies.
    /// </summary>
    public interface IAssemblyLoaderFactory
    {
        /// <summary>
        /// Creates the assembly loader based on the provided
        /// libraries.
        /// </summary>
        /// <param name="runtimeFramework">The <see cref="NuGetFramework"/> that the application is executing in</param>
        /// <param name="loadContextAccessor">A component that can be used to retrieve the current <see cref="IAssemblyLoadContext"/></param>
        /// <param name="dependencies">A <see cref="DependencyManager"/> that can be used to retrieve dependencies</param>
        /// <returns></returns>
        IAssemblyLoader Create(
            NuGetFramework runtimeFramework,
            IAssemblyLoadContextAccessor loadContextAccessor,
            DependencyManager dependencies);
    }
}