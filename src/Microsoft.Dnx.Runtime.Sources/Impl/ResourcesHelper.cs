// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace Microsoft.Dnx.Runtime
{
    internal class ResourcesHelper
    {
        public static bool IsResourceNeutralCulture(AssemblyName assemblyName)
        {
            // CultureName for neutral cultures is empty in windows but on mono CultureName is neutral.
            return string.IsNullOrEmpty(assemblyName.CultureName) || string.Equals(assemblyName.CultureName,"neutral", StringComparison.OrdinalIgnoreCase);
        }
    }
}
