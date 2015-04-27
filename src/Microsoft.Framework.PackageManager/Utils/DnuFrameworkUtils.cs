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
                // Compatibility mapping "aspnetcore/dnxcore -> portable-net45+win8" is missing in NuGet core libs
                // So we need to do that check here
                if (string.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.AspNetCore, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.DnxCore, StringComparison.OrdinalIgnoreCase))
                {
                    nearest = items.FirstOrDefault(x => IsPortableSupportingNet45OrAboveAndWindows(selector(x)));
                }
            }

            return nearest;
        }

        private static bool IsPortableSupportingNet45OrAboveAndWindows(NuGetFramework framework)
        {
            if (!framework.IsPCL)
            {
                return false;
            }

            var frameworkNameProvider = DefaultFrameworkNameProvider.Instance;
            IEnumerable<NuGetFramework> supportedFrameworks;
            if (!frameworkNameProvider.TryGetPortableFrameworks(framework.Profile, out supportedFrameworks))
            {
                return false;
            }

            var supportNet45OrAbove = supportedFrameworks
                .Any(f => string.Equals(f.Framework, FrameworkConstants.FrameworkIdentifiers.Net) &&
                    f.Version >= new Version(4, 5));

            var supportWindows = supportedFrameworks
                .Any(f => string.Equals(f.Framework, FrameworkConstants.FrameworkIdentifiers.Windows));

            return supportNet45OrAbove && supportWindows;
        }
    }
}