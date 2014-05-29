// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using NuGet;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class EmbeddedResourceProvider : IResourceProvider
    {
        public IList<ResourceDescription> GetResources(Project project)
        {
            string root = PathUtility.EnsureTrailingSlash(project.ProjectDirectory);

            // Resources have the relative path from the project root
            // and are separated by /. It's always / regardless of the
            // platform.

            return project.ResourceFiles.Select(resourceFile => new ResourceDescription(
                PathUtility.GetRelativePath(root, resourceFile)
                           .Replace(Path.DirectorySeparatorChar, '/'),
                () => new FileStream(resourceFile, FileMode.Open, FileAccess.Read, FileShare.Read),
                true)).ToList();

        }
    }
}
