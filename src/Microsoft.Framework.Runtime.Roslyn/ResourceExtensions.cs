// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    internal static class ResourceExtensions
    {
        public static void AddRawReferences(this IList<ResourceDescription> resources, IEnumerable<IMetadataRawReference> references)
        {
            foreach (var reference in references)
            {
                resources.Add(new ResourceDescription(reference.Name + ".dll", () =>
                {
                    return new MemoryStream(reference.Contents);
                }, 
                isPublic: true));
            }
        }
    }
}
