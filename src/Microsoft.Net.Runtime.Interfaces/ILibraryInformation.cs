using System.Collections.Generic;

namespace Microsoft.Net.Runtime
{
    [AssemblyNeutral]
    public interface ILibraryInformation
    {
        string Name { get; }

        string Path { get; }

        string Type { get; }

        IEnumerable<string> Dependencies { get; }
    }
}