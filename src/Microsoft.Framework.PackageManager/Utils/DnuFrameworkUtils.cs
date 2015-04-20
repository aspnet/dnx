// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.Framework.PackageManager
{
    internal static class DnuFrameworkUtils
    {
        public static T GetNearest<T>(IEnumerable<T> items, NuGetFramework framework, Func<T, NuGetFramework> selector) where T : class
        {
            var nearest = NuGetFrameworkUtility.GetNearest(items, framework, selector);

            if (nearest == null)
            {
                // The compatibility mapping "dnxcore50 -> portable-net45+win8" is missing in NuGet core libs
                // So we need to do that check here
                if (nearest == null && framework == FrameworkConstants.CommonFrameworks.DnxCore50)
                {
                    nearest = items.FirstOrDefault(x => string.Equals(selector(x).Profile, "Profile7"));
                }
            }

            return nearest;
        }
    }
}