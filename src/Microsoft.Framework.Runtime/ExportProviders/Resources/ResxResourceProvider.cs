// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Xml.Linq;
using Microsoft.Framework.Runtime.Compilation;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class ResxResourceProvider : IResourceProvider
    {
        public IList<ResourceDescriptor> GetResources(ICompilationProject project)
        {
            string root = PathUtility.EnsureTrailingSlash(project.ProjectDirectory);
            return project
                   .Files.ResourceFiles
                   .Where(res => IsResxResourceFile(res))
                   .Select(resxFilePath =>
                        new ResourceDescriptor()
                        {
                            Name = CreateCSharpManifestResourceName.CreateManifestName(
                                 ResourcePathUtility.GetResourceName(root, resxFilePath),
                                 project.Name),
                            StreamFactory = () => GetResourceStream(resxFilePath),
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

            return string.Equals(ext, ".resx", StringComparison.OrdinalIgnoreCase)?
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