using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// Basic package reader that provides the identity, min client version, and file access.
    /// </summary>
    /// <remarks>Higher level concepts used for normal development nupkgs should go at a higher level</remarks>
    public interface IPackageReaderCore : IDisposable
    {
        /// <summary>
        /// Identity of the package
        /// </summary>
        /// <returns></returns>
        PackageIdentity GetIdentity();

        /// <summary>
        /// Minimum client version needed to consume the package.
        /// </summary>
        SemanticVersion GetMinClientVersion();

        /// <summary>
        /// Returns a file stream from the package.
        /// </summary>
        Stream GetStream(string path);

        /// <summary>
        /// All files in the nupkg
        /// </summary>
        IEnumerable<string> GetFiles();

        /// <summary>
        /// Nuspec stream
        /// </summary>
        Stream GetNuspec();
    }
}
