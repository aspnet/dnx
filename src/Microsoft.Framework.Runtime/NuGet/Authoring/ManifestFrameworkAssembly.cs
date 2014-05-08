using System.Xml.Serialization;

namespace NuGet
{
    [XmlType("frameworkAssembly")]
    public class ManifestFrameworkAssembly
    {
        [XmlAttribute("assemblyName")]
        public string AssemblyName { get; set; }

        [XmlAttribute("targetFramework")]
        public string TargetFramework { get; set; }
    }
}
