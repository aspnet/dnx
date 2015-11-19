// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.CompilationAbstractions;
using NuGet;

namespace Microsoft.Dnx.Compilation
{
    public class EmbeddedResourceProvider : IResourceProvider
    {
        public IList<ResourceDescriptor> GetResources(Project project)
        {
            string root = PathUtility.EnsureTrailingSlash(project.ProjectDirectory);
            return project
                   .Files.ResourceFiles
                   .Where(res => !ResxResourceProvider.IsResxResourceFile(res.Key))
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
                           StreamFactory = () => new FileStream(resourceFile.Key, FileMode.Open, FileAccess.Read, FileShare.Read)
                       };
                   })
                   .ToList();
        }
    }
}
