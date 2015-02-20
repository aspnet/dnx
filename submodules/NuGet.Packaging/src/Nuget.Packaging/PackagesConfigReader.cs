using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads packages.config
    /// </summary>
    public class PackagesConfigReader
    {
        private readonly XDocument _doc;
        private readonly IFrameworkNameProvider _frameworkMappings;

        /// <summary>
        /// Packages.config reader
        /// </summary>
        /// <param name="xml">Packages.config XML</param>
        public PackagesConfigReader(XDocument xml)
            : this(DefaultFrameworkNameProvider.Instance, xml)
        {

        }

        public PackagesConfigReader(IFrameworkNameProvider frameworkMappings, XDocument xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException("xml");
            }

            if (frameworkMappings == null)
            {
                throw new ArgumentNullException("frameworkMappings");
            }

            _doc = xml;
            _frameworkMappings = frameworkMappings;
        }

        /// <summary>
        /// Packages.config reader
        /// </summary>
        /// <param name="stream">Stream containing packages.config</param>
        public PackagesConfigReader(Stream stream)
            : this(stream, false)
        {

        }

        /// <summary>
        /// Packages.config reader
        /// </summary>
        /// <param name="stream">Stream containing packages.config</param>
        /// <param name="leaveStreamOpen">True will leave the stream open</param>
        public PackagesConfigReader(Stream stream, bool leaveStreamOpen)
            : this(DefaultFrameworkNameProvider.Instance, stream, leaveStreamOpen)
        {

        }

        /// <summary>
        /// Packages.config reader
        /// </summary>
        /// <param name="stream">Stream containing packages.config</param>
        /// <param name="leaveStreamOpen">True will leave the stream open</param>
        /// <param name="frameworkMappings">Additional target framework mappings for parsing target frameworks</param>
        public PackagesConfigReader(IFrameworkNameProvider frameworkMappings, Stream stream, bool leaveStreamOpen)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            _frameworkMappings = frameworkMappings;
            _doc = XDocument.Load(stream);

            if (!leaveStreamOpen)
            {
                stream.Dispose();
            }
        }

        /// <summary>
        /// Reads the minimum client version from packages.config
        /// </summary>
        /// <returns>Minimum client version or the default of 2.5.0</returns>
        public SemanticVersion GetMinClientVersion()
        {
            SemanticVersion version = null;

            XAttribute node = _doc.Root.Attribute(XName.Get("minClientVersion"));

            if (node == null)
            {
                version = new SemanticVersion(2, 5, 0);
            }
            else if (!SemanticVersion.TryParse(node.Value, out version))
            {
                throw new PackagesConfigReaderException("Invalid minClientVersion");
            }

            return version;
        }

        /// <summary>
        /// Reads all package node entries in the config
        /// </summary>
        /// <returns></returns>
        public IEnumerable<PackageReference> GetPackages()
        {
            List<PackageReference> packages = new List<PackageReference>();

            foreach (var package in _doc.Root.Elements(XName.Get("package")))
            {
                string id = null;

                if (!TryGetAttribute(package, "id", out id) || String.IsNullOrEmpty(id))
                {
                    throw new PackagesConfigReaderException("Invalid package id");
                }

                string version = null;

                if (!TryGetAttribute(package, "version", out version) || String.IsNullOrEmpty(version))
                {
                    throw new PackagesConfigReaderException("Invalid package version");
                }

                NuGetVersion semver = null;

                if (!NuGetVersion.TryParse(version, out semver))
                {
                    throw new PackagesConfigReaderException("Invalid package version");
                }

                string attributeValue = null;

                VersionRange allowedVersions = null;
                if (TryGetAttribute(package, "allowedVersions", out attributeValue))
                {
                    if (!VersionRange.TryParse(attributeValue, out allowedVersions))
                    {
                        throw new PackagesConfigReaderException("Invalid allowedVersions");
                    }
                }

                NuGetFramework targetFramework = null;
                if (TryGetAttribute(package, "targetFramework", out attributeValue))
                {
                    targetFramework = NuGetFramework.Parse(attributeValue, _frameworkMappings);
                }

                bool developmentDependency = BoolAttribute(package, "developmentDependency");
                bool requireReinstallation = BoolAttribute(package, "requireReinstallation");
                bool userInstalled = BoolAttribute(package, "userInstalled", true);

                PackageReference entry = new PackageReference(new PackageIdentity(id, semver), targetFramework, userInstalled, developmentDependency, requireReinstallation, allowedVersions);

                packages.Add(entry);
            }

            return packages;
        }

        // Get a boolean attribute value, or false if it does not exist
        private static bool BoolAttribute(XElement node, string name, bool defaultValue=false)
        {
            string value = null;
            if (TryGetAttribute(node, name, out value))
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(value, "true"))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return defaultValue;
        }

        // Get an attribute that may or may not be present
        private static bool TryGetAttribute(XElement node, string name, out string value)
        {
            var attribute = node.Attributes(XName.Get(name)).SingleOrDefault();

            if (attribute != null && !String.IsNullOrEmpty(attribute.Value))
            {
                value = attribute.Value;
                return true;
            }

            value = null;
            return false;
        }
    }
}
