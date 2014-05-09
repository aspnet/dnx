// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class EmbeddedResourceProvider : IResourceProvider
    {
        public IList<ResourceDescription> GetResources(Project project)
        {
            return project.ResourceFiles.Select(resourceFile => new ResourceDescription(
                Path.GetFileName(resourceFile),
                () => new FileStream(resourceFile, FileMode.Open, FileAccess.Read, FileShare.Read),
                true)).ToList();

        }
    }
}
