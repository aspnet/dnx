using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// A base package reader
    /// </summary>
    public abstract class PackageReaderCoreBase : IPackageReaderCore
    {
        /// <summary>
        /// PackageReaderCore
        /// </summary>
        public PackageReaderCoreBase()
        {

        }

        public virtual PackageIdentity GetIdentity()
        {
            return NuspecCore.GetIdentity();
        }

        public virtual SemanticVersion GetMinClientVersion()
        {
            return NuspecCore.GetMinClientVersion();
        }

        public abstract Stream GetStream(string path);

        public abstract IEnumerable<string> GetFiles();

        public virtual Stream GetNuspec()
        {
            string path = GetFiles().Where(f => f.EndsWith(PackagingCoreConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase)).SingleOrDefault();

            if (String.IsNullOrEmpty(path))
            {
                throw new PackagingException(Strings.MissingNuspec);
            }

            return GetStream(path);
        }

        /// <summary>
        /// Internal low level nuspec reader
        /// </summary>
        /// <remarks>This should be overriden and the higher level nuspec reader returned to avoid parsing
        /// the nuspec multiple times</remarks>
        protected virtual NuspecCoreReaderBase NuspecCore
        {
            get
            {
                return new NuspecCoreReader(GetNuspec());
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            // do nothing here
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
