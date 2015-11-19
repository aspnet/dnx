// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;
using NuGet.Resources;
using NuGet.Xml;

namespace NuGet
{
    public class Manifest
    {
        private const string SchemaVersionAttributeName = "schemaVersion";

        public Manifest(ManifestMetadata metadata)
            : this(metadata, null)
        {
        }

        public Manifest(ManifestMetadata metadata, IEnumerable<ManifestFile> files)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            Metadata = metadata;

            Files = files?.ToList() ?? new List<ManifestFile>();
        }

        public ManifestMetadata Metadata { get; }

        public List<ManifestFile> Files { get; }

        /// <summary>
        /// Saves the current manifest to the specified stream.
        /// </summary>
        /// <param name="stream">The target stream.</param>
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
            var schemaNamespace = (XNamespace)ManifestSchemaUtility.GetSchemaNamespace(version);

            new XDocument(
                new XElement(schemaNamespace + "package",
                    Metadata.ToXElement(schemaNamespace),
                    Files.Any() ?
                        new XElement(schemaNamespace + "files",
                            Files.Select(file => new XElement(schemaNamespace + "file",
                                new XAttribute("src", file.Source),
                                new XAttribute("target", file.Target),
                                new XAttribute("exclude", file.Exclude)))) : null)).Save(stream);
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
            if (rootNameSpace != null && !string.IsNullOrEmpty(rootNameSpace.NamespaceName))
            {
                schemaNamespace = rootNameSpace.NamespaceName;
            }

            return schemaNamespace;
        }

        public static Manifest Create(IPackageMetadata metadata)
        {
            return new Manifest(new ManifestMetadata(metadata));
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
