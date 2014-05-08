using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class ResxResourceProvider : IResourceProvider
    {
        public IList<ResourceDescription> GetResources(Project project)
        {
            return Directory.EnumerateFiles(project.ProjectDirectory, "*.resx", SearchOption.AllDirectories)
                            .Select(resxFilePath =>
                                new ResourceDescription(GetResourceName(project.Name, resxFilePath),
                                                        () => GetResourceStream(resxFilePath),
                                                        isPublic: true)).ToList();
        }

        private static string GetResourceName(string projectName, string resxFilePath)
        {
            Trace.TraceInformation("Found resource {0}", resxFilePath);

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resxFilePath);


            if (fileNameWithoutExtension.StartsWith(projectName, StringComparison.OrdinalIgnoreCase))
            {
                return fileNameWithoutExtension + ".resources";
            }

            return projectName + "." + fileNameWithoutExtension + ".resources";
        }

        private static Stream GetResourceStream(string resxFilePath)
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
