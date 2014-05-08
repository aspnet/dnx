using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface ILibraryManager
    {
        ILibraryExport GetLibraryExport(string name);

        IEnumerable<ILibraryInformation> GetReferencingLibraries(string name);

        ILibraryInformation GetLibraryInformation(string name);

        IEnumerable<ILibraryInformation> GetLibraries();
    }
}
