// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Runtime;

namespace NuGet
{
    public static class VersionExtensions
    {
        public static bool EqualsFloating(this SemanticVersionRange versionRange, SemanticVersion version)
        {
            switch (versionRange.VersionFloatBehavior)
            {
                case SemanticVersionFloatBehavior.Prerelease:
                    return versionRange.MinVersion.Version == version.Version &&
                           version.SpecialVersion.StartsWith(versionRange.MinVersion.SpecialVersion, StringComparison.OrdinalIgnoreCase);

                case SemanticVersionFloatBehavior.Revision:
                    return versionRange.MinVersion.Version.Major == version.Version.Major &&
                           versionRange.MinVersion.Version.Minor == version.Version.Minor &&
                           versionRange.MinVersion.Version.Build == version.Version.Build &&
                           versionRange.MinVersion.Version.Revision == version.Version.Revision;

                case SemanticVersionFloatBehavior.Build:
                    return versionRange.MinVersion.Version.Major == version.Version.Major &&
                           versionRange.MinVersion.Version.Minor == version.Version.Minor &&
                           versionRange.MinVersion.Version.Build == version.Version.Build;

                case SemanticVersionFloatBehavior.Minor:
                    return versionRange.MinVersion.Version.Major == version.Version.Major &&
                           versionRange.MinVersion.Version.Minor == version.Version.Minor;

                case SemanticVersionFloatBehavior.Major:
                    return versionRange.MinVersion.Version.Major == version.Version.Major;

                case SemanticVersionFloatBehavior.None:
                    return versionRange.MinVersion == version;
                default:
                    return false;
            }
        }
    }
}
