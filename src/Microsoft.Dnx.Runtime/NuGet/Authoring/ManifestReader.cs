// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml;
using System.Xml.Linq;
using NuGet.Resources;

namespace NuGet
{
    internal static class ManifestReader
    {
        private static readonly string[] RequiredElements = new string[] { "id", "version", "authors", "description" };

        public static Manifest ReadManifest(XDocument document)
        {
            var metadataElement = document.Root.ElementsNoNamespace("metadata").FirstOrDefault();
            if (metadataElement == null)
            {
                throw new InvalidDataException(
                    string.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredElementMissing, "metadata"));
            }

            return new Manifest(
                ReadMetadata(metadataElement),
                ReadFilesList(document.Root.ElementsNoNamespace("files").FirstOrDefault()));
        }

        private static ManifestMetadata ReadMetadata(XElement xElement)
        {
            var manifestMetadata = new ManifestMetadata();
            manifestMetadata.MinClientVersionString = (string)xElement.Attribute("minClientVersion");

            // we store all child elements under <metadata> so that we can easily check for required elements.
            var allElements = new HashSet<string>();

            foreach (var element in xElement.Elements())
            {
                ReadMetadataValue(manifestMetadata, element, allElements);
            }

            // now check for required elements, which include <id>, <version>, <authors> and <description>
            foreach (var requiredElement in RequiredElements)
            {
                if (!allElements.Contains(requiredElement))
                {
                    throw new InvalidDataException(
                        string.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredElementMissing, requiredElement));
                }
            }

            return manifestMetadata;
        }

        private static void ReadMetadataValue(ManifestMetadata manifestMetadata, XElement element, HashSet<string> allElements)
        {
            if (element.Value == null)
            {
                return;
            }

            allElements.Add(element.Name.LocalName);

            string value = element.Value.SafeTrim();
            switch (element.Name.LocalName)
            {
                case "id":
                    manifestMetadata.Id = value;
                    break;
                case "version":
                    manifestMetadata.Version = new SemanticVersion(value);
                    break;
                case "authors":
                    manifestMetadata.Authors = value?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "owners":
                    manifestMetadata.Owners = value?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "licenseUrl":
                    manifestMetadata.LicenseUrl = new Uri(value);
                    break;
                case "projectUrl":
                    manifestMetadata.ProjectUrl = new Uri(value);
                    break;
                case "iconUrl":
                    manifestMetadata.IconUrl = new Uri(value);
                    break;
                case "requireLicenseAcceptance":
                    manifestMetadata.RequireLicenseAcceptance = XmlConvert.ToBoolean(value);
                    break;
                case "description":
                    manifestMetadata.Description = value;
                    break;
                case "summary":
                    manifestMetadata.Summary = value;
                    break;
                case "releaseNotes":
                    manifestMetadata.ReleaseNotes = value;
                    break;
                case "copyright":
                    manifestMetadata.Copyright = value;
                    break;
                case "language":
                    manifestMetadata.Language = value;
                    break;
                case "title":
                    manifestMetadata.Title = value;
                    break;
                case "tags":
                    manifestMetadata.Tags = value;
                    break;
                case "dependencies":
                    manifestMetadata.DependencySets = ReadDependencySets(element);
                    break;
                case "frameworkAssemblies":
                    manifestMetadata.FrameworkAssemblies = ReadFrameworkAssemblies(element);
                    break;
                case "references":
                    manifestMetadata.PackageAssemblyReferences = ReadReferenceSets(element);
                    break;
            }
        }

        private static List<PackageReferenceSet> ReadReferenceSets(XElement referencesElement)
        {
            if (!referencesElement.HasElements)
            {
                return new List<PackageReferenceSet>(0);
            }

            if (referencesElement.ElementsNoNamespace("group").Any() &&
                referencesElement.ElementsNoNamespace("reference").Any())
            {
                throw new InvalidDataException(NuGetResources.Manifest_ReferencesHasMixedElements);
            }

            var references = ReadReference(referencesElement, throwIfEmpty: false);
            if (references.Any())
            {
                // old format, <reference> is direct child of <references>
                var referenceSet = new PackageReferenceSet(references);
                return new List<PackageReferenceSet> { referenceSet };
            }
            else
            {
                var groups = referencesElement.ElementsNoNamespace("group");
                return groups.Select(element =>
                {
                    var framework = element.GetOptionalAttributeValue("targetFramework")?.Trim();
                    if (framework != null)
                    {
                        return new PackageReferenceSet(VersionUtility.ParseFrameworkName(framework), ReadReference(element, throwIfEmpty: true));
                    }
                    else
                    {
                        return new PackageReferenceSet(ReadReference(element, throwIfEmpty: true));
                    }
                }).ToList();
            }
        }

        public static List<string> ReadReference(XElement referenceElement, bool throwIfEmpty)
        {
            var references = referenceElement.ElementsNoNamespace("reference")
                                             .Select(element => ((string)element.Attribute("file"))?.Trim())
                                             .Where(file => file != null)
                                             .ToList();

            if (throwIfEmpty && references.Count == 0)
            {
                throw new InvalidDataException(NuGetResources.Manifest_ReferencesIsEmpty);
            }

            return references;
        }

        private static List<FrameworkAssemblyReference> ReadFrameworkAssemblies(XElement frameworkElement)
        {
            if (!frameworkElement.HasElements)
            {
                return new List<FrameworkAssemblyReference>(0);
            }

            return frameworkElement.ElementsNoNamespace("frameworkAssembly")
                                   .Where(element => element.Attribute("assemblyName") != null)
                                   .Select(element =>
                                   {
                                       var assemblyName = ((string)element.Attribute("assemblyName")).Trim();
                                       var targetFrameworks = ((string)element.Attribute("targetFramework"))
                                            ?.Trim()
                                            ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(VersionUtility.ParseFrameworkName);

                                       return new FrameworkAssemblyReference(assemblyName, targetFrameworks ?? Enumerable.Empty<FrameworkName>());
                                   })
                                   .ToList();
        }

        private static List<PackageDependencySet> ReadDependencySets(XElement dependenciesElement)
        {
            if (!dependenciesElement.HasElements)
            {
                return new List<PackageDependencySet>();
            }

            // Disallow the <dependencies> element to contain both <dependency> and 
            // <group> child elements. Unfortunately, this cannot be enforced by XSD.
            if (dependenciesElement.ElementsNoNamespace("dependency").Any() &&
                dependenciesElement.ElementsNoNamespace("group").Any())
            {
                throw new InvalidDataException(NuGetResources.Manifest_DependenciesHasMixedElements);
            }

            var dependencies = ReadDependencies(dependenciesElement);
            if (dependencies.Any())
            {
                // old format, <dependency> is direct child of <dependencies>
                var dependencySet = new PackageDependencySet(dependencies);
                return new List<PackageDependencySet> { dependencySet };
            }
            else
            {
                var groups = dependenciesElement.ElementsNoNamespace("group");
                return (from element in groups
                        select new PackageDependencySet(
                            element.GetOptionalAttributeValue("targetFramework")?.Trim(),
                            ReadDependencies(element))
                       ).ToList();
            }
        }

        private static List<PackageDependency> ReadDependencies(XElement containerElement)
        {
            // element is <dependency>
            return (from element in containerElement.ElementsNoNamespace("dependency")
                    let idElement = element.Attribute("id")
                    where idElement != null && !string.IsNullOrEmpty(idElement.Value)
                    select new PackageDependency(
                        idElement.Value?.Trim(),
                        element.GetOptionalAttributeValue("version")?.Trim())
                    ).ToList();
        }

        private static List<ManifestFile> ReadFilesList(XElement xElement)
        {
            if (xElement == null)
            {
                return null;
            }

            List<ManifestFile> files = new List<ManifestFile>();
            foreach (var file in xElement.ElementsNoNamespace("file"))
            {
                var srcElement = file.Attribute("src");
                if (srcElement == null || String.IsNullOrEmpty(srcElement.Value))
                {
                    continue;
                }

                string target = file.GetOptionalAttributeValue("target").SafeTrim();
                string exclude = file.GetOptionalAttributeValue("exclude").SafeTrim();

                // Multiple sources can be specified by using semi-colon separated values. 
                files.AddRange(from source in srcElement.Value.Trim(';').Split(';')
                               select new ManifestFile { Source = source.SafeTrim(), Target = target.SafeTrim(), Exclude = exclude.SafeTrim() });
            }
            return files;
        }
    }
}