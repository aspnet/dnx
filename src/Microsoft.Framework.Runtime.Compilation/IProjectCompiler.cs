using System;
using System.Collections.Generic;
using NuGet.LibraryModel;

namespace Microsoft.Framework.Runtime.Compilation
{
    public interface IProjectCompiler
    {
        IMetadataProjectReference CompileProject(
            Library projectLibrary,
            IEnumerable<ILibraryExport> importedLibraries);
    }
}