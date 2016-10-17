// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class ResourcesForCulture
    {
        public static IEnumerable<ResourceDescriptor> GetResourcesForCulture(string cultureName, IList<ResourceDescriptor> resources)
        {
            var resourcesByCultureName = resources
                .GroupBy(GetResourceCultureName, StringComparer.OrdinalIgnoreCase);

            if (string.Equals(cultureName, "neutral", StringComparison.OrdinalIgnoreCase))
            {
                cultureName = string.Empty;
            }

            return resourcesByCultureName
                .SingleOrDefault(grouping => string.Equals(grouping.Key, cultureName, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetResourceCultureName(ResourceDescriptor res)
        {
            var ext = Path.GetExtension(res.FileName);

            if (string.Equals(ext, ".resx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".restext", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".resources", StringComparison.OrdinalIgnoreCase))
            {
                var resourceBaseName = Path.GetFileNameWithoutExtension(res.FileName);
                var cultureName = Path.GetExtension(resourceBaseName);
                if (string.IsNullOrEmpty(cultureName) || cultureName.Length < 3)
                {
                    return string.Empty;
                }

                // Path.Extension adds a . to the file extension name; example - ".resources". Removing the "." with Substring 
                cultureName = cultureName.Substring(1);

                if (CultureInfoCache.KnownCultureNames.Contains(cultureName))
                {
                    return cultureName;
                }
            }

            return string.Empty;
        }
    }
}
