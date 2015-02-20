using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// Writes the packages.config XML file to a stream
    /// </summary>
    public class PackagesConfigWriter : IDisposable
    {
        private readonly Stream _stream;
        private bool _disposed;
        private bool _closed;
        private readonly List<PackageReference> _entries;
        private NuGetVersion _minClientVersion;
        private IFrameworkNameProvider _frameworkMappings;

        /// <summary>
        /// Create a packages.config writer
        /// </summary>
        /// <param name="stream">Stream to write the XML packages.config file into</param>
        public PackagesConfigWriter(Stream stream)
            : this(DefaultFrameworkNameProvider.Instance, stream)
        {

        }

        public PackagesConfigWriter(IFrameworkNameProvider frameworkMappings, Stream stream)
        {
            _stream = stream;
            _closed = false;
            _entries = new List<PackageReference>();
            _frameworkMappings = frameworkMappings;
        }

        /// <summary>
        /// Write a minimum client version to packages.config
        /// </summary>
        /// <param name="version">Minumum version of the client required to parse and use this file.</param>
        public void WriteMinClientVersion(NuGetVersion version)
        {
            if (_minClientVersion != null)
            {
                throw new PackagingException(String.Format(CultureInfo.InvariantCulture, "MinClientVersion already exists"));
            }

            _minClientVersion = version;
        }

        /// <summary>
        /// Add a package entry
        /// </summary>
        /// <param name="packageId">Package Id</param>
        /// <param name="version">Package Version</param>
        public void WritePackageEntry(string packageId, NuGetVersion version, NuGetFramework targetFramework)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException("packageId");
            }

            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            if (targetFramework == null)
            {
                throw new ArgumentNullException("targetFramework");
            }

            WritePackageEntry(new PackageIdentity(packageId, version), targetFramework);
        }

        /// <summary>
        /// Adds a basic package entry to the file
        /// </summary>
        public void WritePackageEntry(PackageIdentity identity, NuGetFramework targetFramework)
        {
            var entry = new PackageReference(identity, targetFramework);

            WritePackageEntry(entry);
        }

        /// <summary>
        /// Adds a package entry to the file
        /// </summary>
        /// <param name="entry">Package reference entry</param>
        public void WritePackageEntry(PackageReference entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }

            if (_disposed || _closed)
            {
                throw new PackagingException("Writer closed. Unable to add entry.");
            }

            if (_entries.Where(e => StringComparer.OrdinalIgnoreCase.Equals(e.PackageIdentity.Id, entry.PackageIdentity.Id)).Any())
            {
                throw new PackagingException(String.Format(CultureInfo.InvariantCulture, "Package entry already exists. Id: {0}", entry.PackageIdentity.Id));
            }

            _entries.Add(entry);
        }

        private void WriteFile()
        {
            XDocument xml = new XDocument();
            var packages = new XElement(XName.Get("packages"));

            if (_minClientVersion != null)
            {
                XAttribute minClientVersionAttribute = new XAttribute(XName.Get("minClientVersion"), _minClientVersion.ToNormalizedString());
                packages.Add(minClientVersionAttribute);
            }

            xml.Add(packages);

            var sorted = _entries.OrderBy(e => e.PackageIdentity.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in sorted)
            {
                var node = new XElement(XName.Get("package"));

                node.Add(new XAttribute(XName.Get("id"), entry.PackageIdentity.Id));
                node.Add(new XAttribute(XName.Get("version"), entry.PackageIdentity.Version));

                // map the framework to the short name
                // special frameworks such as any and unsupported will be ignored here
                if (entry.TargetFramework.IsSpecificFramework)
                {
                    string frameworkShortName = entry.TargetFramework.GetShortFolderName(_frameworkMappings);

                    if (!String.IsNullOrEmpty(frameworkShortName))
                    {
                        node.Add(new XAttribute(XName.Get("targetFramework"), frameworkShortName));
                    }
                }

                if (entry.HasAllowedVersions)
                {
                    node.Add(new XAttribute(XName.Get("allowedVersions"), entry.AllowedVersions.ToString()));
                }

                if (entry.IsDevelopmentDependency)
                {
                    node.Add(new XAttribute(XName.Get("developmentDependency"), "true"));
                }

                if (entry.RequireReinstallation)
                {
                    node.Add(new XAttribute(XName.Get("requireReinstallation"), "true"));
                }

                if (entry.IsUserInstalled)
                {
                    node.Add(new XAttribute(XName.Get("userInstalled"), "true"));
                }

                packages.Add(node);
            }

            xml.Save(_stream);
        }

        /// <summary>
        /// Write the file to the stream and close it to disallow further changes.
        /// </summary>
        public void Close()
        {
            if (!_closed)
            {
                _closed = true;

                WriteFile();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_closed)
            {
                Close();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Dispose(true);
            }
        }
    }
}
