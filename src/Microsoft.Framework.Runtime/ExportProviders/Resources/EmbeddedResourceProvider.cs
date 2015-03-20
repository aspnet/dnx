// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime.Compilation;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class EmbeddedResourceProvider : IResourceProvider
    {
        public IList<ResourceDescriptor> GetResources(ICompilationProject project)
        {
            string root = PathUtility.EnsureTrailingSlash(project.ProjectDirectory);
            return project
                   .Files.ResourceFiles
                   .Where(res => !ResxResourceProvider.IsResxResourceFile(res))
                   .Select(resourceFile => 
                       new ResourceDescriptor()
                       {
                           Name = CreateCSharpManifestResourceName.CreateManifestName(
                                ResourcePathUtility.GetResourceName(root, resourceFile),
                                project.Name),
                           StreamFactory = () => new FileStream(resourceFile, FileMode.Open, FileAccess.Read, FileShare.Read)
                       })
                   .ToList();
        }
    }
}
