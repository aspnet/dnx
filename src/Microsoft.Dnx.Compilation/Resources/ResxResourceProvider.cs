// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Xml.Linq;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.CompilationAbstractions;
using NuGet;

namespace Microsoft.Dnx.Compilation
{
    public class ResxResourceProvider : IResourceProvider
    {
        public IList<ResourceDescriptor> GetResources(Project project)
        {
            string root = PathUtility.EnsureTrailingSlash(project.ProjectDirectory);
            return project
                   .Files.ResourceFiles
                   .Where(res => IsResxResourceFile(res.Key))
                   .Select(resourceFile =>
                   {
                       string resourceName;
                       string rootNamespace;

                       if (string.IsNullOrEmpty(resourceFile.Value))
                       {
                           // No logical name, so use the file name
                           resourceName = ResourcePathUtility.GetResourceName(root, resourceFile.Key);
                           rootNamespace = project.Name;
                       }
                       else
                       {
                           resourceName = CreateCSharpManifestResourceName.EnsureResourceExtension(resourceFile.Value, resourceFile.Key);
                           rootNamespace = null;
                       }

                       return new ResourceDescriptor()
                       {
                           FileName = Path.GetFileName(resourceName),
                           Name = CreateCSharpManifestResourceName.CreateManifestName(resourceName, rootNamespace),
                           StreamFactory = () => GetResourceStream(resourceFile.Key),
                       };
                   })
                   .ToList();
        }

        public static bool IsResxResourceFile(string fileName)
        {
            var ext = Path.GetExtension(fileName);

            return
                string.Equals(ext, ".resx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".restext", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".resources", StringComparison.OrdinalIgnoreCase);
        }

        private static Stream GetResourceStream(string resxFilePath)
        {
            var ext = Path.GetExtension(resxFilePath);

            return string.Equals(ext, ".resx", StringComparison.OrdinalIgnoreCase) ?
                GetResxResourceStream(resxFilePath) :
                new FileStream(resxFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static Stream GetResxResourceStream(string resxFilePath)
        {
            using (var fs = File.OpenRead(resxFilePath))
            {
                var document = XDocument.Load(fs);

                var ms = new MemoryStream();
                var rw = new ResourceWriter(ms);

                foreach (var e in document.Root.Elements("data"))
                {
                    string name = e.Attribute("name").Value;
                    string value = e.Element("value").Value;

                    rw.AddResource(name, value);
                }

                rw.Generate();
                ms.Seek(0, SeekOrigin.Begin);

                return ms;
            }
        }
    }
}