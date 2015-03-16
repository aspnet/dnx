using System.Collections;
using System.Collections.Generic;
using NuGet.LibraryModel;

namespace Microsoft.Framework.Runtime.Dependencies
{
    public interface IDependencyManager
    {
        IEnumerable<Library> GetLibrariesByType(string type);
    }
}