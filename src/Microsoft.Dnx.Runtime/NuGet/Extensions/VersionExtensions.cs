// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Dnx.Runtime;

namespace NuGet
{
    public static class VersionExtensions
    {
        public static Func<IPackage, bool> ToDelegate(this IVersionSpec versionInfo)
        {
            if (versionInfo == null)
            {
                throw new ArgumentNullException(nameof(versionInfo));
            }
            return versionInfo.ToDelegate<IPackage>(p => p.Version);
        }

        public static Func<T, bool> ToDelegate<T>(this IVersionSpec versionInfo, Func<T, SemanticVersion> extractor)
        {
            if (versionInfo == null)
            {
                throw new ArgumentNullException(nameof(versionInfo));
            }
            if (extractor == null)
            {
                throw new ArgumentNullException(nameof(extractor));
            }

            return p =>
            {
                SemanticVersion version = extractor(p);
                bool condition = true;
                if (versionInfo.MinVersion != null)
                {
                    if (versionInfo.IsMinInclusive)
                    {
                        condition = condition && version >= versionInfo.MinVersion;
                    }
                    else
                    {
                        condition = condition && version > versionInfo.MinVersion;
                    }
                }

                if (versionInfo.MaxVersion != null)
                {
                    if (versionInfo.IsMaxInclusive)
                    {
                        condition = condition && version <= versionInfo.MaxVersion;
                    }
                    else
                    {
                        condition = condition && version < versionInfo.MaxVersion;
                    }
                }

                return condition;
            };
        }

        /// <summary>
        /// Determines if the specified version is within the version spec
        /// </summary>
        public static bool IsSatisfiedBy(this IVersionSpec versionSpec, SemanticVersion version)
        {
            // The range is unbounded so return true
            if (versionSpec == null)
            {
                return true;
            }
            return versionSpec.ToDelegate<SemanticVersion>(v => v)(version);
        }

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

        public static IEnumerable<string> GetComparableVersionStrings(this SemanticVersion version)
        {
            Version coreVersion = version.Version;
            string specialVersion = String.IsNullOrEmpty(version.SpecialVersion) ? String.Empty : "-" + version.SpecialVersion;

            string originalVersion = version.ToString();
            string[] originalVersionComponents = version.GetOriginalVersionComponents();

            var paths = new LinkedList<string>();

            if (coreVersion.Revision == 0)
            {
                if (coreVersion.Build == 0)
                {
                    string twoComponentVersion = String.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.{1}{2}",
                        originalVersionComponents[0],
                        originalVersionComponents[1],
                        specialVersion);

                    AddVersionToList(originalVersion, paths, twoComponentVersion);
                }

                string threeComponentVersion = String.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.{1}.{2}{3}",
                    originalVersionComponents[0],
                    originalVersionComponents[1],
                    originalVersionComponents[2],
                    specialVersion);

                AddVersionToList(originalVersion, paths, threeComponentVersion);
            }

            string fullVersion = String.Format(
                   CultureInfo.InvariantCulture,
                   "{0}.{1}.{2}.{3}{4}",
                   originalVersionComponents[0],
                   originalVersionComponents[1],
                   originalVersionComponents[2],
                   originalVersionComponents[3],
                   specialVersion);

            AddVersionToList(originalVersion, paths, fullVersion);

            return paths;
        }

        private static void AddVersionToList(string originalVersion, LinkedList<string> paths, string nextVersion)
        {
            if (nextVersion.Equals(originalVersion, StringComparison.OrdinalIgnoreCase))
            {
                // IMPORTANT: we want to put the original version string first in the list. 
                // This helps the DataServicePackageRepository reduce the number of requests
                // int the Exists() and FindPackage() methods.
                paths.AddFirst(nextVersion);
            }
            else
            {
                paths.AddLast(nextVersion);
            }
        }
    }
}
