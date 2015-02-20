using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// A development package nuspec reader
    /// </summary>
    public interface INuspecReader : INuspecCoreReader
    {
        IEnumerable<PackageDependencyGroup> GetDependencyGroups();

        IEnumerable<FrameworkSpecificGroup> GetReferenceGroups();

        IEnumerable<FrameworkSpecificGroup> GetFrameworkReferenceGroups();

        /// <summary>
        /// The locale ID for the package, such as en-us.
        /// </summary>
        string GetLanguage();
    }
}
