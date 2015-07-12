// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet
{
    public static class SemanticVersionExtensions
    {
        /// <summary>
        /// Returns true if the original version string is normalized
        ///
        /// For an installed package, it's original version string represents the folder name 
        /// contains the pacakge, the path of which is {id}/{version}/. By checking if the
        /// package's original version string is normalized, it be prevented from failing the 
        /// package path resolving because of a miss version string.
        /// </summary>
        public static bool IsOriginalStringNormalized(this SemanticVersion version)
        {
            return version.GetNormalizedVersionString() == version.OriginalString;
        }
    }
}
