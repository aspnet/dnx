using System.Collections.Generic;
using System.Reflection;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace Microsoft.Framework.Runtime.Loader
{
    /// <summary>
    /// Provides an interface to a loader that can load .NET Assemblies out
    /// of resolved <see cref="Library"/> instances
    /// </summary>
    public interface ILibraryLoader 
    {
        IEnumerable<string> SupportedLibraryTypes { get; }

        void SetResolvedLibraries(IEnumerable<Library> libraries, NuGetFramework runtimeFramework);
        Assembly Load(string name, IAssemblyLoadContext loadContext);
    }
}