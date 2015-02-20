using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// A core package reader that provides an identity and a flat list of dependencies.
    /// </summary>
    public class PackageReaderCore : PackageReaderCoreBase
    {
        private readonly ZipArchive _zip;
        private NuspecCoreReader _nuspec;

        /// <summary>
        /// PackageReaderCore
        /// </summary>
        /// <param name="stream">nupkg zip stream</param>
        public PackageReaderCore(Stream stream)
            : this(stream, false)
        {

        }

        /// <summary>
        /// PackageReaderCore
        /// </summary>
        /// <param name="stream">nupkg zip stream</param>
        /// <param name="leaveStreamOpen">true if the stream should not be disposed of when the package reader is disposed</param>
        public PackageReaderCore(Stream stream, bool leaveStreamOpen)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveStreamOpen);
        }

        public virtual IEnumerable<PackageDependency> GetDependencies()
        {
            return Nuspec.GetDependencies();
        }

        public override Stream GetStream(string path)
        {
            return ZipArchiveHelper.OpenFile(_zip, path);
        }

        public override IEnumerable<string> GetFiles()
        {
            return ZipArchiveHelper.GetFiles(_zip);
        }

        protected sealed override NuspecCoreReaderBase NuspecCore
        {
            get
            {
                return Nuspec;
            }
        }

        protected virtual NuspecCoreReader Nuspec
        {
            get
            {
                if (_nuspec == null)
                {
                    _nuspec = new NuspecCoreReader(GetNuspec());
                }

                return _nuspec;
            }
        }
    }
}
