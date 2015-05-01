// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using NuGet.Resources;

namespace NuGet
{
    [XmlType("package")]
    public class Manifest
    {
        private const string SchemaVersionAttributeName = "schemaVersion";

        public Manifest()
        {
            Metadata = new ManifestMetadata();
        }

        [XmlElement("metadata", IsNullable = false)]
        public ManifestMetadata Metadata { get; set; }

        [XmlArray("files")]
        public List<ManifestFile> Files
        {
            get;
            set;
        }

        public void Save(Stream stream)
        {
            Save(stream, validate: true, minimumManifestVersion: 1);
        }

        /// <summary>
        /// Saves the current manifest to the specified stream.
        /// </summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="minimumManifestVersion">The minimum manifest version that this class must use when saving.</param>
        public void Save(Stream stream, int minimumManifestVersion)
        {
            Save(stream, validate: true, minimumManifestVersion: minimumManifestVersion);
        }

        public void Save(Stream stream, bool validate)
        {
            Save(stream, validate, minimumManifestVersion: 1);
        }

        public void Save(Stream stream, bool validate, int minimumManifestVersion)
        {
            int version = Math.Max(minimumManifestVersion, ManifestVersionUtility.GetManifestVersion(Metadata));
            string schemaNamespace = ManifestSchemaUtility.GetSchemaNamespace(version);

            // Define the namespaces to use when serializing
            var ns = new XmlSerializerNamespaces();
            ns.Add("", schemaNamespace);

            // Need to force the namespace here again as the default in order to get the XML output clean
            var serializer = new XmlSerializer(typeof(Manifest), schemaNamespace);
            serializer.Serialize(stream, this, ns);
        }

        public static Manifest ReadFrom(Stream stream, bool validateSchema)
        {
            return ReadFrom(stream, NullPropertyProvider.Instance, validateSchema);
        }

        public static Manifest ReadFrom(Stream stream, IPropertyProvider propertyProvider, bool validateSchema)
        {
            XDocument document = XmlUtility.LoadSafe(stream, ignoreWhiteSpace: true);

            string schemaNamespace = GetSchemaNamespace(document);
            foreach (var e in document.Descendants())
            {
                // Assign the schema namespace derived to all nodes in the document.
                e.Name = XName.Get(e.Name.LocalName, schemaNamespace);
            }

            // Validate if the schema is a known one
            CheckSchemaVersion(document);

            if (validateSchema)
            {
                // Validate the schema
                ValidateManifestSchema(document, schemaNamespace);
            }

            // Deserialize it
            var manifest = ManifestReader.ReadManifest(document);

            return manifest;
        }

        private static string GetSchemaNamespace(XDocument document)
        {
            string schemaNamespace = ManifestSchemaUtility.SchemaVersionV1;
            var rootNameSpace = document.Root.Name.Namespace;
            if (rootNameSpace != null && !String.IsNullOrEmpty(rootNameSpace.NamespaceName))
            {
                schemaNamespace = rootNameSpace.NamespaceName;
            }
            return schemaNamespace;
        }

        public static Manifest Create(IPackageMetadata metadata)
        {
            return new Manifest
            {
                Metadata = new ManifestMetadata
                {
                    Id = metadata.Id.SafeTrim(),
                    Version = metadata.Version.ToStringSafe(),
                    Title = metadata.Title.SafeTrim(),
                    Authors = GetCommaSeparatedString(metadata.Authors),
                    Owners = GetCommaSeparatedString(metadata.Owners) ?? GetCommaSeparatedString(metadata.Authors),
                    Tags = String.IsNullOrEmpty(metadata.Tags) ? null : metadata.Tags.SafeTrim(),
                    LicenseUrl = ConvertUrlToStringSafe(metadata.LicenseUrl),
                    ProjectUrl = ConvertUrlToStringSafe(metadata.ProjectUrl),
                    IconUrl = ConvertUrlToStringSafe(metadata.IconUrl),
                    RequireLicenseAcceptance = metadata.RequireLicenseAcceptance,
                    // DevelopmentDependency = metadata.DevelopmentDependency,
                    Description = metadata.Description.SafeTrim(),
                    Copyright = metadata.Copyright.SafeTrim(),
                    Summary = metadata.Summary.SafeTrim(),
                    ReleaseNotes = metadata.ReleaseNotes.SafeTrim(),
                    Language = metadata.Language.SafeTrim(),
                    DependencySets = CreateDependencySets(metadata),
                    FrameworkAssemblies = CreateFrameworkAssemblies(metadata),
                    ReferenceSets = CreateReferenceSets(metadata),
                    MinClientVersionString = metadata.MinClientVersion.ToStringSafe()
                },
            };
        }

        private static string ConvertUrlToStringSafe(Uri url)
        {
            if (url != null)
            {
                string originalString = url.OriginalString.SafeTrim();
                if (!String.IsNullOrEmpty(originalString))
                {
                    return originalString;
                }
            }

            return null;
        }

        private static List<ManifestReferenceSet> CreateReferenceSets(IPackageMetadata metadata)
        {
            return (from referenceSet in metadata.PackageAssemblyReferences
                    select new ManifestReferenceSet
                    {
                        TargetFramework = referenceSet.TargetFramework != null ? VersionUtility.GetFrameworkString(referenceSet.TargetFramework) : null,
                        References = CreateReferences(referenceSet)
                    }).ToList();
        }

        private static List<ManifestReference> CreateReferences(PackageReferenceSet referenceSet)
        {
            if (referenceSet.References == null)
            {
                return new List<ManifestReference>();
            }

            return (from reference in referenceSet.References
                    select new ManifestReference { File = reference.SafeTrim() }).ToList();
        }

        private static List<ManifestDependencySet> CreateDependencySets(IPackageMetadata metadata)
        {
            if (metadata.DependencySets.IsEmpty())
            {
                return null;
            }

            return (from dependencySet in metadata.DependencySets
                    select new ManifestDependencySet
                    {
                        TargetFramework = dependencySet.TargetFramework != null ? VersionUtility.GetFrameworkString(dependencySet.TargetFramework) : null,
                        Dependencies = CreateDependencies(dependencySet.Dependencies)
                    }).ToList();
        }

        private static List<ManifestDependency> CreateDependencies(ICollection<PackageDependency> dependencies)
        {
            if (dependencies == null)
            {
                return new List<ManifestDependency>(0);
            }

            return (from dependency in dependencies
                    select new ManifestDependency
                    {
                        Id = dependency.Id.SafeTrim(),
                        Version = dependency.VersionSpec.ToStringSafe()
                    }).ToList();
        }

        private static List<ManifestFrameworkAssembly> CreateFrameworkAssemblies(IPackageMetadata metadata)
        {
            if (metadata.FrameworkAssemblies.IsEmpty())
            {
                return null;
            }
            return (from reference in metadata.FrameworkAssemblies
                    select new ManifestFrameworkAssembly
                    {
                        AssemblyName = reference.AssemblyName,
                        TargetFramework = String.Join(", ", reference.SupportedFrameworks.Select(VersionUtility.GetFrameworkString))
                    }).ToList();
        }

        private static string GetCommaSeparatedString(IEnumerable<string> values)
        {
            if (values == null || !values.Any())
            {
                return null;
            }
            return String.Join(",", values);
        }

        private static void ValidateManifestSchema(XDocument document, string schemaNamespace)
        {
#if DNX451 // CORECLR_TODO: XmlSchema
            var schemaSet = ManifestSchemaUtility.GetManifestSchemaSet(schemaNamespace);

            document.Validate(schemaSet, (sender, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    // Throw an exception if there is a validation error
                    throw new InvalidOperationException(e.Message);
                }
            });
#endif
        }

        private static void CheckSchemaVersion(XDocument document)
        {
#if DNX451 // CORECLR_TODO: XmlSchema
            // Get the metadata node and look for the schemaVersion attribute
            XElement metadata = GetMetadataElement(document);

            if (metadata != null)
            {
                // Yank this attribute since we don't want to have to put it in our xsd
                XAttribute schemaVersionAttribute = metadata.Attribute(SchemaVersionAttributeName);

                if (schemaVersionAttribute != null)
                {
                    schemaVersionAttribute.Remove();
                }

                // Get the package id from the metadata node
                string packageId = GetPackageId(metadata);

                // If the schema of the document doesn't match any of our known schemas
                if (!ManifestSchemaUtility.IsKnownSchema(document.Root.Name.Namespace.NamespaceName))
                {
                    throw new InvalidOperationException(
                            String.Format(CultureInfo.CurrentCulture,
                                          NuGetResources.IncompatibleSchema,
                                          packageId,
                                          typeof(Manifest).Assembly.GetName().Version));
                }
            }
#endif
        }

        private static string GetPackageId(XElement metadataElement)
        {
            XName idName = XName.Get("id", metadataElement.Document.Root.Name.NamespaceName);
            XElement element = metadataElement.Element(idName);

            if (element != null)
            {
                return element.Value;
            }

            return null;
        }

        private static XElement GetMetadataElement(XDocument document)
        {
            // Get the metadata element this way so that we don't have to worry about the schema version
            XName metadataName = XName.Get("metadata", document.Root.Name.Namespace.NamespaceName);

            return document.Root.Element(metadataName);
        }
    }
}
