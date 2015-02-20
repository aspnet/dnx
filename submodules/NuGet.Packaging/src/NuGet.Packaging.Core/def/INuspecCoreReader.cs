using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// A basic nuspec reader that understands ONLY the id, version, and min client version of a package.
    /// </summary>
    /// <remarks>Higher level concepts used for normal development nupkgs should go at a higher level</remarks>
    public interface INuspecCoreReader
    {
        /// <summary>
        /// Package Id
        /// </summary>
        /// <returns></returns>
        string GetId();

        /// <summary>
        /// Package Version
        /// </summary>
        NuGetVersion GetVersion();

        /// <summary>
        /// Minimum client version needed to consume the package.
        /// </summary>
        SemanticVersion GetMinClientVersion();

        /// <summary>
        /// Id and Version of a package.
        /// </summary>
        PackageIdentity GetIdentity();

        /// <summary>
        /// Package metadata in the nuspec
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> GetMetadata();
    }
}
