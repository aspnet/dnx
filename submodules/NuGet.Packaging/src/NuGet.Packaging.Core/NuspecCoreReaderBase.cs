using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// A very basic Nuspec reader that understands the Id, Version, and MinClientVersion of a package.
    /// </summary>
    public abstract class NuspecCoreReaderBase : INuspecCoreReader
    {
        private readonly XDocument _xml;
        private XElement _metadataNode;

        protected const string Metadata = "metadata";
        protected const string Id = "id";
        protected const string Version = "version";
        protected const string MinClientVersion = "minClientVersion";

        /// <summary>
        /// Read a nuspec from a stream.
        /// </summary>
        public NuspecCoreReaderBase(Stream stream)
            : this(XDocument.Load(stream))
        {

        }

        /// <summary>
        /// Reads a nuspec from XML
        /// </summary>
        public NuspecCoreReaderBase(XDocument xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException("xml");
            }

            _xml = xml;
        }

        /// <summary>
        /// Id of the package
        /// </summary>
        public string GetId()
        {
            var node = MetadataNode.Elements(XName.Get(Id, MetadataNode.GetDefaultNamespace().NamespaceName)).SingleOrDefault();
            return node == null ? null : node.Value;
        }

        /// <summary>
        /// Version of the package
        /// </summary>
        public NuGetVersion GetVersion()
        {
            var node = MetadataNode.Elements(XName.Get(Version, MetadataNode.GetDefaultNamespace().NamespaceName)).SingleOrDefault();
            return node == null ? null : NuGetVersion.Parse(node.Value);
        }

        /// <summary>
        /// The minimum client version this package supports.
        /// </summary>
        public SemanticVersion GetMinClientVersion()
        {
            var node = MetadataNode.Attribute(XName.Get(MinClientVersion));
            return node == null ? null : SemanticVersion.Parse(node.Value);
        }

        /// <summary>
        /// Nuspec Metadata
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> GetMetadata()
        {
            foreach (var element in MetadataNode.Elements().Where(n => !n.HasElements && !String.IsNullOrEmpty(n.Value)))
            {
                yield return new KeyValuePair<string, string>(element.Name.LocalName, element.Value);
            }

            yield break;
        }

        protected XElement MetadataNode
        {
            get
            {
                if (_metadataNode == null)
                {
                    // find the metadata node regardless of the NS, some legacy packages have the NS here instead of on package
                    _metadataNode = _xml.Root.Elements().Where(e => StringComparer.Ordinal.Equals(e.Name.LocalName, Metadata)).SingleOrDefault();

                    if (_metadataNode == null)
                    {
                        // TODO: add a resource string for this
                        throw new InvalidOperationException();
                    }
                }

                return _metadataNode;
            }
        }

        /// <summary>
        /// Raw XML doc
        /// </summary>
        public XDocument Xml
        {
            get
            {
                return _xml;
            }
        }


        public PackageIdentity GetIdentity()
        {
            return new PackageIdentity(GetId(), GetVersion());
        }
    }
}
