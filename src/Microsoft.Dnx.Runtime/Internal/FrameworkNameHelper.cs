// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Runtime.Helpers
{
    public static class FrameworkNameHelper
    {
        public static FrameworkName ParseFrameworkName(string targetFramework)
        {
            if (targetFramework.IndexOf(',') != -1)
            {
                // Assume it's a framework name if it contains commas
                // e.g. .NETPortable,Version=v4.5,Profile=Profile78
                return new FrameworkName(targetFramework);
            }

            return VersionUtility.ParseFrameworkName(targetFramework);
        }

        public static string MakeDefaultTargetFrameworkDefine(Tuple<string, FrameworkName> frameworkDefinition)
        {
            var shortName = frameworkDefinition.Item1;
            var targetFramework = frameworkDefinition.Item2;

            if (targetFramework.IsPortableFramework())
            {
                return null;
            }

            return shortName.ToUpperInvariant();
        }
    }
}