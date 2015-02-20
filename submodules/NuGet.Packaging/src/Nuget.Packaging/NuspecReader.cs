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
    /// Reads .nuspec files
    /// </summary>
    public class NuspecReader : NuspecCoreReaderBase
    {
        // node names
        protected const string Dependencies = "dependencies";
        protected const string Group = "group";
        protected const string TargetFramework = "targetFramework";
        protected const string Dependency = "dependency";
        protected const string References = "references";
        protected const string Reference = "reference";
        protected const string File = "file";
        protected const string FrameworkAssemblies = "frameworkAssemblies";
        protected const string FrameworkAssembly = "frameworkAssembly";
        protected const string AssemblyName = "assemblyName";
        protected const string Language = "language";

        public NuspecReader(Stream stream)
            : base(stream)
        {

        }

        public NuspecReader(XDocument xml)
            : base(xml)
        {

        }

        public IEnumerable<PackageDependencyGroup> GetDependencyGroups()
        {
            string ns = MetadataNode.GetDefaultNamespace().NamespaceName;

            bool groupFound = false;

            foreach (var depGroup in MetadataNode.Elements(XName.Get(Dependencies, ns)).Elements(XName.Get(Group, ns)))
            {
                groupFound = true;

                string groupFramework = GetAttributeValue(depGroup, TargetFramework);

                List<PackageDependency> packages = new List<PackageDependency>();

                foreach (var depNode in depGroup.Elements(XName.Get(Dependency, ns)))
                {
                    VersionRange range = null;

                    var rangeNode = GetAttributeValue(depNode, Version);

                    if (!String.IsNullOrEmpty(rangeNode))
                    {
                        if (!VersionRange.TryParse(rangeNode, out range))
                        {
                            // TODO: error handling
                        }
                    }

                    packages.Add(new PackageDependency(GetAttributeValue(depNode, Id), range));
                }

                yield return new PackageDependencyGroup(groupFramework, packages);
            }

            // legacy behavior
            if (!groupFound)
            {
                var packages = MetadataNode.Elements(XName.Get(Dependencies, ns))
                    .Elements(XName.Get(Dependency, ns)).Select(n => new PackageDependency(GetAttributeValue(n, Id), VersionRange.Parse(GetAttributeValue(n, Version)))).ToArray();

                if (packages.Any())
                {
                    yield return new PackageDependencyGroup(NuGetFramework.AnyFramework, packages);
                }
            }

            yield break;
        }

        public IEnumerable<FrameworkSpecificGroup> GetReferenceGroups()
        {
            string ns = MetadataNode.GetDefaultNamespace().NamespaceName;

            bool groupFound = false;

            foreach (var group in MetadataNode.Elements(XName.Get(References, ns)).Elements(XName.Get(Group, ns)))
            {
                groupFound = true;

                string groupFramework = GetAttributeValue(group, TargetFramework);

                string[] items = group.Elements(XName.Get(Reference, ns)).Select(n => GetAttributeValue(n, File)).Where(n => !String.IsNullOrEmpty(n)).ToArray();

                if (items.Length > 0)
                {
                    yield return new FrameworkSpecificGroup(groupFramework, items);
                }
            }

            // pre-2.5 flat list of references, this should only be used if there are no groups
            if (!groupFound)
            {
                string[] items = MetadataNode.Elements(XName.Get(References, ns))
                    .Elements(XName.Get(Reference, ns)).Select(n => GetAttributeValue(n, File)).Where(n => !String.IsNullOrEmpty(n)).ToArray();

                if (items.Length > 0)
                {
                    yield return new FrameworkSpecificGroup(NuGetFramework.AnyFramework, items);
                }
            }

            yield break;
        }

        public IEnumerable<FrameworkSpecificGroup> GetFrameworkReferenceGroups()
        {
            string ns = Xml.Root.GetDefaultNamespace().NamespaceName;

            foreach (var group in MetadataNode.Elements(XName.Get(FrameworkAssemblies, ns)).Elements(XName.Get(FrameworkAssembly, ns))
                .GroupBy(n => GetAttributeValue(n, TargetFramework)))
            {
                yield return new FrameworkSpecificGroup(group.Key, group.Select(n => GetAttributeValue(n, AssemblyName)).Where(n => !String.IsNullOrEmpty(n)).ToArray());
            }
        }

        public string GetLanguage()
        {
            var node = MetadataNode.Elements(XName.Get(Language, MetadataNode.GetDefaultNamespace().NamespaceName)).SingleOrDefault();
            return node == null ? null : node.Value;
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            XAttribute attribute = element.Attribute(XName.Get(attributeName));
            return attribute == null ? null : attribute.Value;
        }
    }
}
