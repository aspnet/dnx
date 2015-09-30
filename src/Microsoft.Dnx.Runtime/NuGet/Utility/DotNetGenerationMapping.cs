using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace NuGet
{
    internal static class DotNetGenerationMapping
    {
        private static readonly Version _version45 = new Version(4, 5);
        private static readonly Dictionary<FrameworkName, Version> _generationMappings = new Dictionary<FrameworkName, Version>()
        {
            // dnxcore50
            { new FrameworkName(VersionUtility.DnxCoreFrameworkIdentifier, new Version(5, 0)), new Version(5, 5) },

            // netcore50/uap10
            { new FrameworkName(VersionUtility.NetCoreFrameworkIdentifier, new Version(5, 0)), new Version(5, 4) },
            { new FrameworkName(VersionUtility.UapFrameworkIdentifier, new Version(10, 0)), new Version(5, 4) },

            // netN
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5)), new Version(5, 2) },
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5, 1)), new Version(5, 3) },
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 5, 2)), new Version(5, 3) },
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 6)), new Version(5, 4) },
            { new FrameworkName(VersionUtility.NetFrameworkIdentifier, new Version(4, 6, 1)), new Version(5, 5) },

            // dnxN
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5)), new Version(5, 2) },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5, 1)), new Version(5, 3) },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 5, 2)), new Version(5, 3) },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 6)), new Version(5, 4) },
            { new FrameworkName(VersionUtility.DnxFrameworkIdentifier, new Version(4, 6, 1)), new Version(5, 5) },

            // winN
            { new FrameworkName(VersionUtility.WindowsFrameworkIdentifier, new Version(8, 0)), new Version(5, 2) },
            { new FrameworkName(VersionUtility.NetCoreFrameworkIdentifier, new Version(4, 5)), new Version(5, 2) },
            { new FrameworkName(VersionUtility.WindowsFrameworkIdentifier, new Version(8, 1)), new Version(5, 3) },
            { new FrameworkName(VersionUtility.NetCoreFrameworkIdentifier, new Version(4, 5, 1)), new Version(5, 3) },

            // windows phone silverlight
            { new FrameworkName(VersionUtility.WindowsPhoneFrameworkIdentifier, new Version(8, 0)), new Version(5, 1) },
            { new FrameworkName(VersionUtility.WindowsPhoneFrameworkIdentifier, new Version(8, 1)), new Version(5, 1) },
            { new FrameworkName(VersionUtility.SilverlightFrameworkIdentifier, new Version(8, 0), VersionUtility.WindowsPhoneFrameworkIdentifier), new Version(5, 1) },

            // wpaN
            { new FrameworkName(VersionUtility.WindowsPhoneAppFrameworkIdentifier, new Version(8, 1)), new Version(5, 3) }
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

            yield return input;

            var gen = GetGeneration(input);

            // dnxN -> netN -> dotnetY
            if (input.Identifier.Equals(VersionUtility.DnxFrameworkIdentifier))
            {
                yield return new FrameworkName(VersionUtility.NetFrameworkIdentifier, input.Version);
                if (gen != null)
                {
                    yield return gen;
                }
            }
            // uap10 -> netcore50 -> wpa81 -> dotnetY
            else if (input.Identifier.Equals(VersionUtility.UapFrameworkIdentifier) && input.Version == Microsoft.Dnx.Runtime.Constants.Version10_0)
            {
                yield return new FrameworkName(VersionUtility.NetCoreFrameworkIdentifier, Microsoft.Dnx.Runtime.Constants.Version50);
                yield return new FrameworkName(VersionUtility.WindowsPhoneAppFrameworkIdentifier, new Version(8, 1));
                if (gen != null)
                {
                    yield return gen;
                }
            }
            // netcore50 (universal windows apps) -> wpa81 -> dotnetY
            else if (input.Identifier.Equals(VersionUtility.NetCoreFrameworkIdentifier) && input.Version == Microsoft.Dnx.Runtime.Constants.Version50)
            {
                yield return new FrameworkName(VersionUtility.WindowsPhoneAppFrameworkIdentifier, new Version(8, 1));
                if (gen != null)
                {
                    yield return gen;
                }
            }
            // others just map to a generation (if any)
            else if (gen != null)
            {
                yield return gen;
            }
        }

        public static FrameworkName GetGeneration(FrameworkName input)
        {
            Version version;
            if (!_generationMappings.TryGetValue(input, out version))
            {
                return null;
            }
            return new FrameworkName(VersionUtility.NetPlatformFrameworkIdentifier, version);
        }
    }
}
