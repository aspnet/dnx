using System.Xml.Serialization;

namespace NuGet
{
    [XmlType("dependency")]
    public class ManifestDependency
    {
        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("version")]
        public string Version { get; set; }
    }
}