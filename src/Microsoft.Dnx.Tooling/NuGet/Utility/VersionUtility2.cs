using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using CompatibilityMapping = System.Collections.Generic.Dictionary<string, string[]>;

namespace NuGet
{
    public static class VersionUtility2
    {
        private static readonly Version _version5 = new Version(5, 0);

        private static readonly Dictionary<string, CompatibilityMapping> _compatibiltyMapping = new Dictionary<string, CompatibilityMapping>(StringComparer.OrdinalIgnoreCase) {
            {
                // Client profile is compatible with the full framework (empty string is full)
                VersionUtility.NetFrameworkIdentifier, new CompatibilityMapping(StringComparer.OrdinalIgnoreCase) {
                    { "", new [] { "Client" } },
                    { "Client", new [] { "" } }
                }
            },
            {
                "Silverlight", new CompatibilityMapping(StringComparer.OrdinalIgnoreCase) {
                    { "WindowsPhone", new[] { "WindowsPhone71" } },
                    { "WindowsPhone71", new[] { "WindowsPhone" } }
                }
            }
        };

        public static bool IsPackageBased(FrameworkName framework)
        {
            return
                string.Equals(framework.Identifier, VersionUtility.DnxCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(framework.Identifier, VersionUtility.AspNetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(framework.Identifier, VersionUtility.NetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) && framework.Version >= _version5);
        }

        public static Version NormalizeVersion(Version verison)
        {
            return new Version(verison.Major,
                               verison.Minor,
                               Math.Max(verison.Build, 0),
                               Math.Max(verison.Revision, 0));
        }

        public static FrameworkName GetNearest(FrameworkName projectFramework, IEnumerable<FrameworkName> items)
        {
            IEnumerable<FrameworkName> names;
            if (GetNearestCore(projectFramework, items, i => new[] { i }, out names))
            {
                return names.FirstOrDefault();
            }
            return null;
        }

        public static bool GetNearest<T>(FrameworkName projectFramework, IEnumerable<T> items, out IEnumerable<T> compatibleItems) where T : IFrameworkTargetable
        {
            return GetNearestCore(projectFramework, items, i => i.SupportedFrameworks, out compatibleItems);
        }

        private static bool GetNearestCore<T>(FrameworkName projectFramework, IEnumerable<T> items, Func<T, IEnumerable<FrameworkName>> fxExtractor, out IEnumerable<T> compatibleItems)
        {
            if (!items.Any())
            {
                compatibleItems = Enumerable.Empty<T>();
                return true;
            }

            // Not all projects have a framework, we need to consider those projects.
            var internalProjectFramework = projectFramework ?? VersionUtility.EmptyFramework;

            // Turn something that looks like this:
            // item -> [Framework1, Framework2, Framework3] into
            // [{item, Framework1}, {item, Framework2}, {item, Framework3}]
            var normalizedItems = from item in items
                                  let supported = fxExtractor(item)
                                  let frameworks = (supported != null && supported.Any()) ? supported : new FrameworkName[] { null }
                                  from framework in frameworks
                                  select new
                                  {
                                      Item = item,
                                      TargetFramework = framework
                                  };

            // Group references by target framework (if there is no target framework we assume it is the default)
            var frameworkGroups = normalizedItems.GroupBy(g => g.TargetFramework, g => g.Item).ToList();

            if (!projectFramework.IsPortableFramework())
            {
                // Find exact matching items in expansion order.
                foreach (var activeFramework in Expand(internalProjectFramework))
                {
                    var matchingGroups = frameworkGroups.Where(g =>
                        string.Equals(g.Key?.Identifier, activeFramework.Identifier, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(g.Key?.Profile, activeFramework.Profile, StringComparison.OrdinalIgnoreCase)).ToList();
                    var bestGroup = matchingGroups
                        .OrderByDescending(f => f.Key.Version)
                        .FirstOrDefault(g => g.Key.Version <= activeFramework.Version);
                    if (bestGroup != null)
                    {
                        compatibleItems = bestGroup;
                        return true;
                    }
                }
            }

            // Try the old way
            return TryGetCompatibleItemsCore(out compatibleItems, internalProjectFramework, frameworkGroups);
        }

        private static bool TryGetCompatibleItems<T>(FrameworkName projectFramework, IEnumerable<T> items, out IEnumerable<T> compatibleItems) where T : IFrameworkTargetable
        {
            if (!items.Any())
            {
                compatibleItems = Enumerable.Empty<T>();
                return true;
            }

            // Not all projects have a framework, we need to consider those projects.
            var internalProjectFramework = projectFramework ?? VersionUtility.EmptyFramework;

            // Turn something that looks like this:
            // item -> [Framework1, Framework2, Framework3] into
            // [{item, Framework1}, {item, Framework2}, {item, Framework3}]
            var normalizedItems = from item in items
                                  let frameworks = (item.SupportedFrameworks != null && item.SupportedFrameworks.Any()) ? item.SupportedFrameworks : new FrameworkName[] { null }
                                  from framework in frameworks
                                  select new
                                  {
                                      Item = item,
                                      TargetFramework = framework
                                  };

            // Group references by target framework (if there is no target framework we assume it is the default)
            var frameworkGroups = normalizedItems.GroupBy(g => g.TargetFramework, g => g.Item).ToList();

            return TryGetCompatibleItemsCore(out compatibleItems, internalProjectFramework, frameworkGroups);
        }

        private static bool TryGetCompatibleItemsCore<T>(out IEnumerable<T> compatibleItems, FrameworkName internalProjectFramework, List<IGrouping<FrameworkName, T>> frameworkGroups)
        {
            // Try to find the best match
            // Not all projects have a framework, we need to consider those projects.
            compatibleItems = (from g in frameworkGroups
                               where g.Key != null && IsCompatible(internalProjectFramework, g.Key)
                               orderby GetProfileCompatibility(internalProjectFramework, g.Key) descending
                               select g).FirstOrDefault();

            bool hasItems = compatibleItems != null && compatibleItems.Any();
            if (!hasItems)
            {
                // if there's no matching profile, fall back to the items without target framework
                // because those are considered to be compatible with any target framework
                compatibleItems = frameworkGroups.Where(g => g.Key == null).SelectMany(g => g);
                hasItems = compatibleItems != null && compatibleItems.Any();
            }

            if (!hasItems)
            {
                compatibleItems = null;
            }

            return hasItems;
        }

        /// <summary>
        /// Determines if a package's target framework can be installed into a project's framework.
        /// </summary>
        /// <param name="frameworkName">The project's framework</param>
        /// <param name="targetFrameworkName">The package's target framework</param>
        public static bool IsCompatible(FrameworkName frameworkName, FrameworkName targetFrameworkName)
        {
            if (frameworkName == null)
            {
                return true;
            }

            // Treat portable library specially
            if (targetFrameworkName.IsPortableFramework())
            {
                return IsPortableLibraryCompatible(frameworkName, targetFrameworkName);
            }

            targetFrameworkName = VersionUtility.NormalizeFrameworkName(targetFrameworkName);
            frameworkName = VersionUtility.NormalizeFrameworkName(frameworkName);

            frameworkName = Expand(frameworkName)
                .FirstOrDefault(fx => String.Equals(fx.Identifier, targetFrameworkName.Identifier, StringComparison.OrdinalIgnoreCase));
            if (frameworkName == null)
            {
                return false;
            }

            if (NormalizeVersion(frameworkName.Version) <
                NormalizeVersion(targetFrameworkName.Version))
            {
                return false;
            }

            // If the profile names are equal then they're compatible
            if (String.Equals(frameworkName.Profile, targetFrameworkName.Profile, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Get the compatibility mapping for this framework identifier
            CompatibilityMapping mapping;
            if (_compatibiltyMapping.TryGetValue(frameworkName.Identifier, out mapping))
            {
                // Get all compatible profiles for the target profile
                string[] compatibleProfiles;
                if (mapping.TryGetValue(targetFrameworkName.Profile, out compatibleProfiles))
                {
                    // See if this profile is in the list of compatible profiles
                    return compatibleProfiles.Contains(frameworkName.Profile, StringComparer.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        private static IEnumerable<FrameworkName> Expand(FrameworkName input)
        {
            // Try to convert the project framework into an equivalent target framework
            // If the identifiers didn't match, we need to see if this framework has an equivalent framework that DOES match.
            // If it does, we use that from here on.
            // Example:
            //  If the Project Targets ASP.Net, Version=5.0. It can accept Packages targetting .NETFramework, Version=4.5.1
            //  so since the identifiers don't match, we need to "translate" the project target framework to .NETFramework
            //  however, we still want direct ASP.Net == ASP.Net matches, so we do this ONLY if the identifiers don't already match

            yield return input;

            // dnxcoreN -> aspnetcoreN -> dotnetN
            if (input.Identifier.Equals(VersionUtility.DnxCoreFrameworkIdentifier))
            {
                yield return new FrameworkName(VersionUtility.AspNetCoreFrameworkIdentifier, input.Version);
                yield return new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, input.Version);
            }
            // aspnetcoreN -> dotnetN
            else if (input.Identifier.Equals(VersionUtility.AspNetCoreFrameworkIdentifier))
            {
                yield return new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, input.Version);
            }
            // dnxN -> aspnet50 -> netN
            else if (input.Identifier.Equals(VersionUtility.DnxFrameworkIdentifier))
            {
                yield return new FrameworkName(VersionUtility.AspNetFrameworkIdentifier, new Version(5, 0));
                yield return new FrameworkName(VersionUtility.NetFrameworkIdentifier, input.Version);
                yield return new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 0));
            }
            // aspnet50 -> net46 (project framework; this is DEPRECATED, so setting a max version is OK)
            else if (input.Identifier.Equals(VersionUtility.AspNetFrameworkIdentifier))
            {
                yield return new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 6));
                yield return new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 0));
            }
            // netcore50 (universal windows apps) -> dotnet50
            else if (input.Identifier.Equals(VersionUtility.NetCoreFrameworkIdentifier) && input.Version.Major == 5 && input.Version.Minor == 0)
            {
                yield return new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 0));
            }
            // net45 -> dotnet
            else if (input.Identifier.Equals(VersionUtility.NetFrameworkIdentifier) && input.Version >= new Version(4, 5))
            {
                yield return new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 0));
            }
        }

        private static bool IsPortableLibraryCompatible(FrameworkName frameworkName, FrameworkName targetFrameworkName)
        {
            if (String.IsNullOrEmpty(targetFrameworkName.Profile))
            {
                return false;
            }

            NetPortableProfile targetFrameworkPortableProfile = NetPortableProfile.Parse(targetFrameworkName.Profile);
            if (targetFrameworkPortableProfile == null)
            {
                return false;
            }

            if (frameworkName.IsPortableFramework())
            {
                // this is the case with Portable Library vs. Portable Library
                if (String.Equals(frameworkName.Profile, targetFrameworkName.Profile, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                NetPortableProfile frameworkPortableProfile = NetPortableProfile.Parse(frameworkName.Profile);
                if (frameworkPortableProfile == null)
                {
                    return false;
                }

                return targetFrameworkPortableProfile.IsCompatibleWith(frameworkPortableProfile);
            }
            else
            {
                // this is the case with Portable Library installed into a normal project
                bool isCompatible = targetFrameworkPortableProfile.IsCompatibleWith(frameworkName);

                if (!isCompatible)
                {
                    // TODO: Remove this logic when out dependencies have moved to ASP.NET Core 5.0
                    // as this logic is super fuzzy and terrible
                    if (string.Equals(frameworkName.Identifier, VersionUtility.AspNetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(frameworkName.Identifier, VersionUtility.DnxCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        (string.Equals(frameworkName.Identifier, VersionUtility.NetPlatformFrameworkIdentifier, StringComparison.OrdinalIgnoreCase)))
                    {
                        var frameworkIdentifierLookup = targetFrameworkPortableProfile.SupportedFrameworks
                                                                                      .Select(VersionUtility.NormalizeFrameworkName)
                                                                                      .ToLookup(f => f.Identifier);

                        if (frameworkIdentifierLookup[VersionUtility.NetFrameworkIdentifier].Any(f => f.Version >= new Version(4, 5)) &&
                            frameworkIdentifierLookup[VersionUtility.NetCoreFrameworkIdentifier].Any(f => f.Version >= new Version(4, 5)))
                        {
                            return true;
                        }
                    }
                }

                return isCompatible;
            }
        }

        /// <summary>
        /// Given 2 framework names, this method returns a number which determines how compatible
        /// the names are. The higher the number the more compatible the frameworks are.
        /// </summary>
        private static long GetProfileCompatibility(FrameworkName frameworkName, FrameworkName targetFrameworkName)
        {
            frameworkName = VersionUtility.NormalizeFrameworkName(frameworkName);
            targetFrameworkName = VersionUtility.NormalizeFrameworkName(targetFrameworkName);

            if (targetFrameworkName.IsPortableFramework())
            {
                if (frameworkName.IsPortableFramework())
                {
                    return GetCompatibilityBetweenPortableLibraryAndPortableLibrary(frameworkName, targetFrameworkName);
                }
                else
                {
                    // we divide by 2 to ensure Portable framework has less compatibility value than specific framework.
                    return GetCompatibilityBetweenPortableLibraryAndNonPortableLibrary(frameworkName, targetFrameworkName) / 2;
                }
            }

            long compatibility = 0;

            // Calculate the "distance" between the target framework version and the project framework version.
            // When comparing two framework candidates, we pick the one with higher version.
            compatibility += CalculateVersionDistance(
                frameworkName.Version,
                GetEffectiveFrameworkVersion(frameworkName, targetFrameworkName));

            // Things with matching profiles are more compatible than things without.
            // This means that if we have net40 and net40-client assemblies and the target framework is
            // net40, both sets of assemblies are compatible but we prefer net40 since it matches
            // the profile exactly.
            if (targetFrameworkName.Profile.Equals(frameworkName.Profile, StringComparison.OrdinalIgnoreCase))
            {
                compatibility++;
            }

            // this is to give specific profile higher compatibility than portable profile
            if (targetFrameworkName.Identifier.Equals(frameworkName.Identifier, StringComparison.OrdinalIgnoreCase))
            {
                // Let's say a package has two framework folders: 'net40' and 'portable-net45+wp8'.
                // The package is installed into a net45 project. We want to pick the 'net40' folder, even though
                // the 'net45' in portable folder has a matching version with the project's framework.
                //
                // So, in order to achieve that, here we give the folder that has matching identifer with the project's
                // framework identifier a compatibility score of 10, to make sure it weighs more than the compatibility of matching version.

                compatibility += 10 * (1L << 32);
            }

            return compatibility;
        }

        private static long CalculateVersionDistance(Version projectVersion, Version targetFrameworkVersion)
        {
            // the +5 is to counter the profile compatibility increment (+1)
            const long MaxValue = 1L << 32 + 5;

            // calculate the "distance" between 2 versions
            var distance = (projectVersion.Major - targetFrameworkVersion.Major) * 255L * 255 * 255 +
                           (projectVersion.Minor - targetFrameworkVersion.Minor) * 255L * 255 +
                           (projectVersion.Build - targetFrameworkVersion.Build) * 255L +
                           (projectVersion.Revision - targetFrameworkVersion.Revision);

            Debug.Assert(MaxValue >= distance);

            // the closer the versions are, the higher the returned value is.
            return MaxValue - distance;
        }

        private static Version GetEffectiveFrameworkVersion(FrameworkName projectFramework, FrameworkName targetFrameworkVersion)
        {
            if (targetFrameworkVersion.IsPortableFramework())
            {
                NetPortableProfile profile = NetPortableProfile.Parse(targetFrameworkVersion.Profile);
                if (profile != null)
                {
                    // if it's a portable library, return the version of the matching framework
                    var compatibleFramework = profile.SupportedFrameworks.FirstOrDefault(f => VersionUtility2.IsCompatible(projectFramework, f));
                    if (compatibleFramework != null)
                    {
                        return compatibleFramework.Version;
                    }
                }
            }

            return targetFrameworkVersion.Version;
        }

        /// <summary>
        /// Attempt to calculate how compatible a portable framework folder is to a portable project.
        /// The two portable frameworks passed to this method MUST be compatible with each other.
        /// </summary>
        /// <remarks>
        /// The returned score will be negative value.
        /// </remarks>
        internal static int GetCompatibilityBetweenPortableLibraryAndPortableLibrary(FrameworkName frameworkName, FrameworkName targetFrameworkName)
        {
            // Algorithms: Give a score from 0 to N indicating how close *in version* each package platform is the project's platforms
            // and then choose the folder with the lowest score. If the score matches, choose the one with the least platforms.
            //
            // For example:
            //
            // Project targeting: .NET 4.5 + SL5 + WP71
            //
            // Package targeting:
            // .NET 4.5 (0) + SL5 (0) + WP71 (0)                            == 0
            // .NET 4.5 (0) + SL5 (0) + WP71 (0) + Win8 (0)                 == 0
            // .NET 4.5 (0) + SL4 (1) + WP71 (0) + Win8 (0)                 == 1
            // .NET 4.0 (1) + SL4 (1) + WP71 (0) + Win8 (0)                 == 2
            // .NET 4.0 (1) + SL4 (1) + WP70 (1) + Win8 (0)                 == 3
            //
            // Above, thereâ€™s two matches with the same result, choose the one with the least amount of platforms.
            //
            // There will be situations, however, where there is still undefined behavior, such as:
            //
            // .NET 4.5 (0) + SL4 (1) + WP71 (0)                            == 1
            // .NET 4.0 (1) + SL5 (0) + WP71 (0)                            == 1

            NetPortableProfile frameworkProfile = NetPortableProfile.Parse(frameworkName.Profile);
            Debug.Assert(frameworkName != null);

            NetPortableProfile targetFrameworkProfile = NetPortableProfile.Parse(targetFrameworkName.Profile);
            Debug.Assert(targetFrameworkName != null);

            int score = 0;
            foreach (var framework in targetFrameworkProfile.SupportedFrameworks)
            {
                var matchingFramework = frameworkProfile.SupportedFrameworks.FirstOrDefault(f => IsCompatible(f, framework));
                if (matchingFramework != null && matchingFramework.Version > framework.Version)
                {
                    score++;
                }
            }

            // This is to ensure that if two portable frameworks have the same score,
            // we pick the one that has less number of supported platforms.
            score = score * 50 + targetFrameworkProfile.SupportedFrameworks.Count;

            // Our algorithm returns lowest score for the most compatible framework.
            // However, the caller of this method expects it to have the highest score.
            // Hence, we return the negative value of score here.
            return -score;
        }

        internal static long GetCompatibilityBetweenPortableLibraryAndNonPortableLibrary(FrameworkName frameworkName, FrameworkName portableFramework)
        {
            NetPortableProfile profile = NetPortableProfile.Parse(portableFramework.Profile);
            if (profile == null)
            {
                // defensive coding, this should never happen
                Debug.Assert(false, "'portableFramework' is not a valid portable framework.");
                return 0;
            }

            // among the supported frameworks by the Portable library, pick the one that is compatible with 'frameworkName'
            var compatibleFramework = profile.SupportedFrameworks.FirstOrDefault(f => IsCompatible(frameworkName, f));

            if (compatibleFramework != null)
            {
                var score = GetProfileCompatibility(frameworkName, compatibleFramework);

                // This is to ensure that if two portable frameworks have the same score,
                // we pick the one that has less number of supported platforms.
                // The *2 is to make up for the /2 to which the result of this method is subject.
                score -= (profile.SupportedFrameworks.Count * 2);

                return score;
            }

            return 0;
        }

        public static bool ShouldUseConsidering(
            SemanticVersion current,
            SemanticVersion considering,
            SemanticVersionRange ideal)
        {
            if (considering == null)
            {
                // skip nulls
                return false;
            }

            if (!ideal.EqualsFloating(considering) && considering < ideal.MinVersion)
            {
                // Don't use anything that can't be satisfied
                return false;
            }

            if (ideal.MaxVersion != null)
            {
                if (ideal.IsMaxInclusive && considering > ideal.MaxVersion)
                {
                    return false;
                }
                else if (ideal.IsMaxInclusive == false && considering >= ideal.MaxVersion)
                {
                    return false;
                }
            }

            /*
            Come back to this later
            if (ideal.VersionFloatBehavior == SemanticVersionFloatBehavior.None &&
                considering != ideal.MinVersion)
            {
                return false;
            }
            */

            if (current == null)
            {
                // always use version when it's the first valid
                return true;
            }

            if (ideal.EqualsFloating(current) &&
                ideal.EqualsFloating(considering))
            {
                // favor higher version when they both match a floating pattern
                return current < considering;
            }

            // Favor lower versions
            return current > considering;
        }

    }
}
