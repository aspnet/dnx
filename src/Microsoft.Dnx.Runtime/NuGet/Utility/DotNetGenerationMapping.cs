using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet
{
    internal static class DotNetGenerationMapping
    {
        private static readonly Version _version45 = new Version(4, 5);
        private static readonly Dictionary<FrameworkName, Version> _generationMappings = new Dictionary<FrameworkName, Version>()
        {
            // netcoredapp
            { new FrameworkName(VersionUtility.NetCoreAppFrameworkIdentifier, new Version(1, 0)), new Version(1, 5) },

            // netstandardapp
            { new FrameworkName(VersionUtility.NetStandardAppFrameworkIdentifier, new Version(1, 5)), new Version(1, 5) },

            // dotnet
            { new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 1)), new Version(1, 0) },
            { new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 2)), new Version(1, 1) },
            { new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 3)), new Version(1, 2) },
            { new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 4)), new Version(1, 3) },
            { new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, 5)), new Version(1, 4) },

            // dnxcore50
            { new FrameworkName(VersionUtility.DnxCoreFrameworkIdentifier, new Version(5, 0)), new Version(1, 5) },

            // netcore50/uap10
            { new FrameworkName(VersionUtility.NetCoreFrameworkIdentifier, new Version(5, 0)), new Version(1, 3) },
            { new FrameworkName(VersionUtility.UapFrameworkIdentifier, new Version(10, 0)), new Version(1, 3) },

            // netN
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5)), new Version(1, 1) },
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5, 1)), new Version(1, 2) },
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5, 2)), new Version(1, 2) },
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 6)), new Version(1, 3) },
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 6, 1)), new Version(1, 4) },

            // dnxN
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5)), new Version(1, 1) },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5, 1)), new Version(1, 2) },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5, 2)), new Version(1, 2) },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 6)), new Version(1, 3) },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 6, 1)), new Version(1, 4) },

            // winN
            { new FrameworkName(VersionUtility.WindowsFrameworkIdentifier, new Version(8, 0)), new Version(1, 1) },
            { new FrameworkName(VersionUtility.NetCoreFrameworkIdentifier, new Version(4, 5)), new Version(1, 1) },
            { new FrameworkName(VersionUtility.WindowsFrameworkIdentifier, new Version(8, 1)), new Version(1, 2) },
            { new FrameworkName(VersionUtility.NetCoreFrameworkIdentifier, new Version(4, 5, 1)), new Version(1, 2) },

            // windows phone silverlight
            { new FrameworkName(VersionUtility.WindowsPhoneFrameworkIdentifier, new Version(8, 0)), new Version(1, 0) },
            { new FrameworkName(VersionUtility.WindowsPhoneFrameworkIdentifier, new Version(8, 1)), new Version(1, 0) },
            { new FrameworkName(VersionUtility.SilverlightFrameworkIdentifier, new Version(8, 0), VersionUtility.WindowsPhoneFrameworkIdentifier), new Version(1, 0) },

            // wpaN
            { new FrameworkName(VersionUtility.WindowsPhoneAppFrameworkIdentifier, new Version(8, 1)), new Version(1, 2) }
        };

        public static IEnumerable<FrameworkName> Expand(FrameworkName input)
        {
            // Try to convert the project framework into an equivalent target framework
            // If the identifiers didn't match, we need to see if this framework has an equivalent framework that DOES match.
            // If it does, we use that from here on.
            // Example:
            //  If the Project Targets DNX, Version=4.5.1. It can accept Packages targetting .NETFramework, Version=4.5.1
            //  so since the identifiers don't match, we need to "translate" the project target framework to .NETFramework
            //  however, we still want direct DNX == DNX matches, so we do this ONLY if the identifiers don't already match
            // This also handles .NET Generation mappings


            var candidates = new Queue<FrameworkName>();
            candidates.Enqueue(input);
            var results = new HashSet<FrameworkName>();
            while (candidates.Any())
            {
                var candidate = candidates.Dequeue();
                results.Add(candidate);

                foreach (var name in ExpandOnce(candidate))
                {
                    if (!results.Contains(name))
                    {
                        candidates.Enqueue(name);
                    }
                }
            }
            return results;
        }

        private static IEnumerable<FrameworkName> ExpandOnce(FrameworkName candidate)
        {
            var gen = GetGeneration(candidate);
            foreach (var frameworkName in gen)
            {
                yield return frameworkName;
            }

            // netstandard -> dotnet
            if (candidate.Identifier.Equals(VersionUtility.NetStandardFrameworkIdentifier))
            {
                yield return new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, new Version(5, candidate.Version.Minor + 1));
            }
            // netstandardapp -> dnxcore
            else if (candidate.Identifier.Equals(VersionUtility.NetStandardAppFrameworkIdentifier) && candidate.Version == new Version(1, 5))
            {
                yield return new FrameworkName(VersionUtility.DnxCoreFrameworkIdentifier, Microsoft.Dnx.Runtime.Constants.Version50);
            }
            // netcoreapp -> dnxcore
            else if (candidate.Identifier.Equals(VersionUtility.NetCoreAppFrameworkIdentifier) && candidate.Version == new Version(1, 0))
            {
                yield return new FrameworkName(VersionUtility.DnxCoreFrameworkIdentifier, Microsoft.Dnx.Runtime.Constants.Version50);
            }
            // dnxcore -> netstandardapp
            else if (candidate.Identifier.Equals(VersionUtility.DnxCoreFrameworkIdentifier) && candidate.Version == Microsoft.Dnx.Runtime.Constants.Version50)
            {
                yield return new FrameworkName(VersionUtility.NetStandardAppFrameworkIdentifier, new Version(1, 5));
                yield return new FrameworkName(VersionUtility.NetCoreAppFrameworkIdentifier, new Version(1, 0));
            }
            // dnxN -> netN -> dotnetY
            else if (candidate.Identifier.Equals(VersionUtility.DnxFrameworkIdentifier))
            {
                yield return new FrameworkName(VersionUtility.NetFrameworkIdentifier, candidate.Version);
            }
            // uap10 -> netcore50 -> wpa81 -> dotnetY
            else if (candidate.Identifier.Equals(VersionUtility.UapFrameworkIdentifier) && candidate.Version == Microsoft.Dnx.Runtime.Constants.Version10_0)
            {
                yield return new FrameworkName(VersionUtility.NetCoreFrameworkIdentifier, Microsoft.Dnx.Runtime.Constants.Version50);
                yield return new FrameworkName(VersionUtility.WindowsPhoneAppFrameworkIdentifier, new Version(8, 1));
            }
            // netcore50 (universal windows apps) -> wpa81 -> dotnetY
            else if (candidate.Identifier.Equals(VersionUtility.NetCoreFrameworkIdentifier) && candidate.Version == Microsoft.Dnx.Runtime.Constants.Version50)
            {
                yield return new FrameworkName(VersionUtility.WindowsPhoneAppFrameworkIdentifier, new Version(8, 1));
            }
        }

        public static IEnumerable<FrameworkName> GetGeneration(FrameworkName input)
        {
            Version version;
            if (!_generationMappings.TryGetValue(input, out version))
            {
                yield break;
            }
            yield return new FrameworkName(VersionUtility.NetStandardFrameworkIdentifier, version);
        }
    }
}
