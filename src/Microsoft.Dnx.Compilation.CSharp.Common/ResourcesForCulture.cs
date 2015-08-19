// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class ResourcesForCulture
    {
        public static IEnumerable<ResourceDescriptor> GetResourcesForCulture(string cultureName, IList<ResourceDescriptor> resources)
        {
            var resourcesByCultureName = resources
                .GroupBy(GetResourceCultureName, StringComparer.OrdinalIgnoreCase);

            return resourcesByCultureName
                .SingleOrDefault(grouping => string.Equals(grouping.Key, cultureName, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetResourceCultureName(ResourceDescriptor res)
        {
            var resourceBaseName = Path.GetFileNameWithoutExtension(res.FileName);
            var cultureName = Path.GetExtension(resourceBaseName);
            if (string.IsNullOrEmpty(cultureName) || cultureName.Length < 3)
            {
                return string.Empty;
            }
            bool previousCharWasDash = false;
            for (var index = 1; index != cultureName.Length; ++index)
            {
                var ch = cultureName[index];
                var isDash = ch == '-';
                var isAlpha = !isDash && ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'));
                var isDigit = !isDash && !isAlpha && (ch >= '0' && ch <= '9');

                if (isDash && previousCharWasDash)
                {
                    // two '-' in a row is not valid
                    return string.Empty;
                }

                if (index < 3)
                {
                    if (!isAlpha)
                    {
                        // first characters at [1] and [2] must be alpha
                        return string.Empty;
                    }
                }
                else
                {
                    if (!isAlpha && !isDigit && !isDash)
                    {
                        // not an allowed character
                        return string.Empty;
                    }
                }

                previousCharWasDash = isDash;
            }
            if (previousCharWasDash)
            {
                // trailing '-' is not valid
                return string.Empty;
            }
            return cultureName.Substring(1);
        }
    }
}
