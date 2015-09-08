// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.Impl;
using NuGet.Resources;

namespace NuGet
{
    public static class VersionUtility
    {
        public static readonly string AspNetCoreFrameworkIdentifier = "Asp.NetCore";
        public static readonly string DnxCoreFrameworkIdentifier = "DNXCore";
        public static readonly string PortableFrameworkIdentifier = ".NETPortable";
        public static readonly string NetPlatformFrameworkIdentifier = ".NETPlatform";
        public static readonly string NetCoreFrameworkIdentifier = ".NETCore";

        public const string NetFrameworkIdentifier = ".NETFramework";
        public const string AspNetFrameworkIdentifier = "Asp.Net";
        public const string DnxFrameworkIdentifier = "DNX";
        public const string DnxFrameworkShortName = "dnx";
        public const string DnxCoreFrameworkShortName = "dnxcore";
        public const string NetPlatformFrameworkShortName = "dotnet";

        private const string LessThanOrEqualTo = "\u2264";
        private const string GreaterThanOrEqualTo = "\u2265";

        public static readonly FrameworkName EmptyFramework = new FrameworkName("NoFramework", new Version(0, 0));
        public static readonly FrameworkName UnsupportedFrameworkName = new FrameworkName("Unsupported", new Version(0, 0));

        private static readonly Version _emptyVersion = new Version(0, 0);

        private static readonly IDictionary<string, string> _knownIdentifiers = PopulateKnownFrameworks();

        private static readonly Dictionary<string, string> _knownProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "Client", "Client" },
            { "WP", "WindowsPhone" },
            { "WP71", "WindowsPhone71" },
            { "CF", "CompactFramework" },
            { "Full", String.Empty }
        };

        private static readonly Dictionary<string, string> _identifierToFrameworkFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { NetFrameworkIdentifier, "net" },
            { ".NETMicroFramework", "netmf" },
            { DnxFrameworkIdentifier, DnxFrameworkShortName },
            { DnxCoreFrameworkIdentifier, DnxCoreFrameworkShortName },
            { AspNetFrameworkIdentifier, "aspnet" },
            { AspNetCoreFrameworkIdentifier, "aspnetcore" },
            { NetPlatformFrameworkIdentifier, NetPlatformFrameworkShortName },

            { "Silverlight", "sl" },
            { ".NETCore", "win"},
            { "Windows", "win"},
            { ".NETPortable", "portable" },
            { "WindowsPhone", "wp"},
            { "WindowsPhoneApp", "wpa"}
        };

        private static readonly Dictionary<string, string> _identifierToProfileFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "WindowsPhone", "wp" },
            { "WindowsPhone71", "wp71" },
            { "CompactFramework", "cf" }
        };

        // These aliases allow us to accept 'wp', 'wp70', 'wp71', 'windows', 'windows8' as valid target farmework folders.
        private static readonly Dictionary<FrameworkName, FrameworkName> _frameworkNameAlias = new Dictionary<FrameworkName, FrameworkName>(FrameworkNameEqualityComparer.Default)
        {
            { new FrameworkName("WindowsPhone, Version=v0.0"), new FrameworkName("Silverlight, Version=v3.0, Profile=WindowsPhone") },
            { new FrameworkName("WindowsPhone, Version=v7.0"), new FrameworkName("Silverlight, Version=v3.0, Profile=WindowsPhone") },
            { new FrameworkName("WindowsPhone, Version=v7.1"), new FrameworkName("Silverlight, Version=v4.0, Profile=WindowsPhone71") },
            { new FrameworkName("WindowsPhone, Version=v8.0"), new FrameworkName("Silverlight, Version=v8.0, Profile=WindowsPhone") },

            { new FrameworkName("Windows, Version=v0.0"), new FrameworkName(".NETCore, Version=v4.5") },
            { new FrameworkName("Windows, Version=v8.0"), new FrameworkName(".NETCore, Version=v4.5") },
            { new FrameworkName("Windows, Version=v8.1"), new FrameworkName(".NETCore, Version=v4.5.1") }
        };

        // This method must be kep in sync with CompilerOptionsExtensions.IsDesktop
        public static bool IsDesktop(FrameworkName frameworkName)
        {
            return frameworkName.Identifier == NetFrameworkIdentifier ||
                   frameworkName.Identifier == AspNetFrameworkIdentifier ||
                   frameworkName.Identifier == DnxFrameworkIdentifier;
        }

        /// <summary>
        /// This function tries to normalize a string that represents framework version names into
        /// something a framework name that the package manager understands.
        /// </summary>
        public static FrameworkName ParseFrameworkName(string frameworkName)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException(nameof(frameworkName));
            }

            // TODO: Intern these frameworks
            // Fast path for runtime code path, these 3 short names are the runnable tfms
            // We fall back to regular parsing in other scenarios (build/dth)
            if (frameworkName == FrameworkNames.ShortNames.Dnx451)
            {
                return new FrameworkName(FrameworkNames.LongNames.Dnx, new Version(4, 5, 1));
            }
            else if (frameworkName == FrameworkNames.ShortNames.Dnx46)
            {
                return new FrameworkName(FrameworkNames.LongNames.Dnx, new Version(4, 6));
            }
            else if (frameworkName == FrameworkNames.ShortNames.DnxCore50)
            {
                return new FrameworkName(FrameworkNames.LongNames.DnxCore, new Version(5, 0));
            }

            // {Identifier}{Version}-{Profile}

            // Split the framework name into 3 parts, identifier, version and profile.
            string identifierPart = null;
            string versionPart = null;

            string[] parts = frameworkName.Split('-');

            if (parts.Length > 2)
            {
                throw new ArgumentException(NuGetResources.InvalidFrameworkNameFormat, nameof(frameworkName));
            }

            string frameworkNameAndVersion = parts.Length > 0 ? parts[0].Trim() : null;
            string profilePart = parts.Length > 1 ? parts[1].Trim() : null;

            if (String.IsNullOrEmpty(frameworkNameAndVersion))
            {
                throw new ArgumentException(NuGetResources.MissingFrameworkName, nameof(frameworkName));
            }

            // If we find a version then we try to split the framework name into 2 parts
            int versionIndex = -1;
            var versionMatch = frameworkNameAndVersion.FirstOrDefault(c =>
            {
                versionIndex++;
                return char.IsDigit(c);
            });

            if (versionMatch != '\0' && versionIndex != -1)
            {
                identifierPart = frameworkNameAndVersion.Substring(0, versionIndex).Trim();
                versionPart = frameworkNameAndVersion.Substring(versionIndex).Trim();
            }
            else
            {
                // Otherwise we take the whole name as an identifier
                identifierPart = frameworkNameAndVersion.Trim();
            }

            if (!String.IsNullOrEmpty(identifierPart))
            {
                // Try to normalize the identifier to a known identifier
                if (!_knownIdentifiers.TryGetValue(identifierPart, out identifierPart))
                {
                    return UnsupportedFrameworkName;
                }
            }

            if (!String.IsNullOrEmpty(profilePart))
            {
                string knownProfile;
                if (_knownProfiles.TryGetValue(profilePart, out knownProfile))
                {
                    profilePart = knownProfile;
                }
            }

            Version version = null;
            // We support version formats that are integers (40 becomes 4.0)
            int versionNumber;
            if (Int32.TryParse(versionPart, out versionNumber))
            {
                // Remove the extra numbers
                if (versionPart.Length > 4)
                {
                    versionPart = versionPart.Substring(0, 4);
                }

                // Make sure it has at least 2 digits so it parses as a valid version

                versionPart = versionPart.PadRight(2, '0');
                versionPart = String.Join(".", versionPart.ToCharArray());
            }

            // If we can't parse the version then use the default
            if (!Version.TryParse(versionPart, out version))
            {
                // We failed to parse the version string once more. So we need to decide if this is unsupported or if we use the default version.
                // This framework is unsupported if:
                // 1. The identifier part of the framework name is null.
                // 2. The version part is not null.
                if (String.IsNullOrEmpty(identifierPart) || !String.IsNullOrEmpty(versionPart))
                {
                    return UnsupportedFrameworkName;
                }

                version = identifierPart.Equals(NetPlatformFrameworkIdentifier) ? new Version(5, 0) : _emptyVersion;
            }

            if (String.IsNullOrEmpty(identifierPart))
            {
                identifierPart = NetFrameworkIdentifier;
            }

            // if this is a .NET Portable framework name, validate the profile part to ensure it is valid
            if (identifierPart.Equals(PortableFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                ValidatePortableFrameworkProfilePart(profilePart);
            }

            return new FrameworkName(identifierPart, version, profilePart);
        }

        public static void ValidatePortableFrameworkProfilePart(string profilePart)
        {
            if (String.IsNullOrEmpty(profilePart))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileEmpty, nameof(profilePart));
            }

            if (profilePart.Contains('-'))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileHasDash, nameof(profilePart));
            }

            if (profilePart.Contains(' '))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileHasSpace, nameof(profilePart));
            }

            string[] parts = profilePart.Split('+');
            if (parts.Any(p => String.IsNullOrEmpty(p)))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileComponentIsEmpty, nameof(profilePart));
            }

            // Prevent portable framework inside a portable framework - Inception
            if (parts.Any(p => p.StartsWith("portable", StringComparison.OrdinalIgnoreCase)) ||
                parts.Any(p => p.StartsWith("NETPortable", StringComparison.OrdinalIgnoreCase)) ||
                parts.Any(p => p.StartsWith(".NETPortable", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileComponentIsPortable, nameof(profilePart));
            }
        }

        public static SemanticVersionRange ParseVersionRange(string value)
        {
            var floatBehavior = SemanticVersionFloatBehavior.None;

            // Support snapshot versions
            if (value.EndsWith("-*"))
            {
                floatBehavior = SemanticVersionFloatBehavior.Prerelease;
                value = value.Substring(0, value.Length - 2);
            }

            var spec = ParseVersionSpec(value);

            return new SemanticVersionRange(spec)
            {
                VersionFloatBehavior = floatBehavior
            };
        }

        /// <summary>
        /// The version string is either a simple version or an arithmetic range
        /// e.g.
        ///      1.0         --> 1.0 â‰¤ x
        ///      (,1.0]      --> x â‰¤ 1.0
        ///      (,1.0)      --> x &lt; 1.0
        ///      [1.0]       --> x == 1.0
        ///      (1.0,)      --> 1.0 &lt; x
        ///      (1.0, 2.0)   --> 1.0 &lt; x &lt; 2.0
        ///      [1.0, 2.0]   --> 1.0 â‰¤ x â‰¤ 2.0
        /// </summary>
        public static IVersionSpec ParseVersionSpec(string value)
        {
            IVersionSpec versionInfo;
            if (!TryParseVersionSpec(value, out versionInfo))
            {
                throw new ArgumentException(
                    String.Format(CultureInfo.CurrentCulture,
                     NuGetResources.InvalidVersionString, value));
            }

            return versionInfo;
        }

        private static bool TryParseVersionSpec(string value, out IVersionSpec result)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            value = value.Trim();

            // First, try to parse it as a plain version string
            SemanticVersion version;
            if (SemanticVersion.TryParse(value, out version))
            {
                // A plain version is treated as an inclusive minimum range
                result = new VersionSpec
                {
                    MinVersion = version,
                    IsMinInclusive = true
                };

                return true;
            }

            // It's not a plain version, so it must be using the bracket arithmetic range syntax

            result = null;

            // Fail early if the string is too short to be valid
            if (value.Length < 3)
            {
                return false;
            }

            var versionSpec = new VersionSpec();
            // The first character must be [ ot (
            switch (value.First())
            {
                case '[':
                    versionSpec.IsMinInclusive = true;
                    break;
                case '(':
                    versionSpec.IsMinInclusive = false;
                    break;
                default:
                    return false;
            }

            // The last character must be ] ot )
            switch (value.Last())
            {
                case ']':
                    versionSpec.IsMaxInclusive = true;
                    break;
                case ')':
                    versionSpec.IsMaxInclusive = false;
                    break;
                default:
                    return false;
            }

            // Get rid of the two brackets
            value = value.Substring(1, value.Length - 2);

            // Split by comma, and make sure we don't get more than two pieces
            string[] parts = value.Split(',');
            if (parts.Length > 2)
            {
                return false;
            }
            else if (parts.All(String.IsNullOrEmpty))
            {
                // If all parts are empty, then neither of upper or lower bounds were specified. Version spec is of the format (,]
                return false;
            }

            // If there is only one piece, we use it for both min and max
            string minVersionString = parts[0];
            string maxVersionString = (parts.Length == 2) ? parts[1] : parts[0];

            // Only parse the min version if it's non-empty
            if (!String.IsNullOrWhiteSpace(minVersionString))
            {
                if (!TryParseVersion(minVersionString, out version))
                {
                    return false;
                }
                versionSpec.MinVersion = version;
            }

            // Same deal for max
            if (!String.IsNullOrWhiteSpace(maxVersionString))
            {
                if (!TryParseVersion(maxVersionString, out version))
                {
                    return false;
                }
                versionSpec.MaxVersion = version;
            }

            // Successful parse!
            result = versionSpec;
            return true;
        }

        internal static string PrettyPrint(IVersionSpec versionSpec)
        {
            if (versionSpec.MinVersion != null && versionSpec.IsMinInclusive && versionSpec.MaxVersion == null && !versionSpec.IsMaxInclusive)
            {
                return String.Format(CultureInfo.InvariantCulture, "({0} {1})", GreaterThanOrEqualTo, versionSpec.MinVersion);
            }

            if (versionSpec.MinVersion != null && versionSpec.MaxVersion != null && versionSpec.MinVersion == versionSpec.MaxVersion && versionSpec.IsMinInclusive && versionSpec.IsMaxInclusive)
            {
                return String.Format(CultureInfo.InvariantCulture, "(= {0})", versionSpec.MinVersion);
            }

            var versionBuilder = new StringBuilder();
            if (versionSpec.MinVersion != null)
            {
                if (versionSpec.IsMinInclusive)
                {
                    versionBuilder.AppendFormat(CultureInfo.InvariantCulture, "({0} ", GreaterThanOrEqualTo);
                }
                else
                {
                    versionBuilder.Append("(> ");
                }
                versionBuilder.Append(versionSpec.MinVersion);
            }

            if (versionSpec.MaxVersion != null)
            {
                if (versionBuilder.Length == 0)
                {
                    versionBuilder.Append("(");
                }
                else
                {
                    versionBuilder.Append(" && ");
                }

                if (versionSpec.IsMaxInclusive)
                {
                    versionBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0} ", LessThanOrEqualTo);
                }
                else
                {
                    versionBuilder.Append("< ");
                }
                versionBuilder.Append(versionSpec.MaxVersion);
            }

            if (versionBuilder.Length > 0)
            {
                versionBuilder.Append(")");
            }

            return versionBuilder.ToString();
        }

        public static string GetFrameworkString(FrameworkName frameworkName)
        {
            string name = frameworkName.Identifier + frameworkName.Version;
            if (String.IsNullOrEmpty(frameworkName.Profile))
            {
                return name;
            }
            return name + "-" + frameworkName.Profile;
        }

        public static string GetShortFrameworkName(FrameworkName frameworkName)
        {
            // Do a reverse lookup in _frameworkNameAlias. This is so that we can produce the more user-friendly
            // "windowsphone" string, rather than "sl3-wp". The latter one is also prohibited in portable framework's profile string.
            foreach (KeyValuePair<FrameworkName, FrameworkName> pair in _frameworkNameAlias)
            {
                // use our custom equality comparer because we want to perform case-insensitive comparison
                if (FrameworkNameEqualityComparer.Default.Equals(pair.Value, frameworkName))
                {
                    frameworkName = pair.Key;
                    break;
                }
            }

            // Normalize version 5.0 to 0.0 for display purposes FOR NetPlatform
            if (frameworkName.Identifier.Equals(NetPlatformFrameworkIdentifier) && frameworkName.Version == new Version(5, 0))
            {
                frameworkName = new FrameworkName(frameworkName.Identifier, _emptyVersion, frameworkName.Profile);
            }

            string name;
            if (!_identifierToFrameworkFolder.TryGetValue(frameworkName.Identifier, out name))
            {
                name = frameworkName.Identifier;
            }

            // for Portable framework name, the short name has the form "portable-sl4+wp7+net45"
            string profile;
            // only show version part if it's > 0.0.0.0
            if (frameworkName.Version > new Version(0, 0))
            {
                // Remove the . from versions
                name += frameworkName.Version.ToString().Replace(".", String.Empty);
            }

            if (String.IsNullOrEmpty(frameworkName.Profile))
            {
                return name;
            }

            if (!_identifierToProfileFolder.TryGetValue(frameworkName.Profile, out profile))
            {
                profile = frameworkName.Profile;
            }

            return name + "-" + profile;
        }

        public static FrameworkName ParseFrameworkNameFromFilePath(string filePath, out string effectivePath)
        {
            var knownFolders = new string[]
            {
                Constants.ContentDirectory,
                Constants.LibDirectory,
                Constants.ToolsDirectory,
                Constants.BuildDirectory
            };

            for (int i = 0; i < knownFolders.Length; i++)
            {
                string folderPrefix = knownFolders[i] + System.IO.Path.DirectorySeparatorChar;
                if (filePath.Length > folderPrefix.Length &&
                    filePath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string frameworkPart = filePath.Substring(folderPrefix.Length);

                    try
                    {
                        return VersionUtility.ParseFrameworkFolderName(
                            frameworkPart,
                            strictParsing: knownFolders[i] == Constants.LibDirectory,
                            effectivePath: out effectivePath);
                    }
                    catch (ArgumentException)
                    {
                        // if the parsing fails, we treat it as if this file
                        // doesn't have target framework.
                        effectivePath = frameworkPart;
                        return null;
                    }
                }

            }

            effectivePath = filePath;
            return null;
        }

        public static bool IsPortableFramework(this FrameworkName framework)
        {
            // .NETPortable 5.0+ is dramatically different from previous versions of .NETPortable,
            // so we return false here if the framework is .NETPortable 5.0+ since the new versions
            // of portable behave more like normal frameworks than portable profiles.

            // The profile part has been verified in the ParseFrameworkName() method.
            // By the time it is called here, it's guaranteed to be valid.
            // Thus we can ignore the profile part here
            return framework != null &&
                PortableFrameworkIdentifier.Equals(framework.Identifier, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses the specified string into FrameworkName object.
        /// </summary>
        /// <param name="path">The string to be parse.</param>
        /// <param name="strictParsing">if set to <c>true</c>, parse the first folder of path even if it is unrecognized framework.</param>
        /// <param name="effectivePath">returns the path after the parsed target framework</param>
        /// <returns></returns>
        private static FrameworkName ParseFrameworkFolderName(string path, bool strictParsing, out string effectivePath)
        {
            // The path for a reference might look like this for assembly foo.dll:
            // foo.dll
            // sub\foo.dll
            // {FrameworkName}{Version}\foo.dll
            // {FrameworkName}{Version}\sub1\foo.dll
            // {FrameworkName}{Version}\sub1\sub2\foo.dll

            // Get the target framework string if specified
            string targetFrameworkString = Path.GetDirectoryName(path).Split(Path.DirectorySeparatorChar).First();

            effectivePath = path;

            if (String.IsNullOrEmpty(targetFrameworkString))
            {
                return null;
            }

            var targetFramework = ParseFrameworkName(targetFrameworkString);
            if (strictParsing || targetFramework != UnsupportedFrameworkName)
            {
                // skip past the framework folder and the character \
                effectivePath = path.Substring(targetFrameworkString.Length + 1);
                return targetFramework;
            }

            return null;
        }

        public static FrameworkName NormalizeFrameworkName(FrameworkName framework)
        {
            FrameworkName aliasFramework;
            if (_frameworkNameAlias.TryGetValue(framework, out aliasFramework))
            {
                return aliasFramework;
            }

            return framework;
        }

        private static bool TryParseVersion(string versionString, out SemanticVersion version)
        {
            version = null;
            if (!SemanticVersion.TryParse(versionString, out version))
            {
                // Support integer version numbers (i.e. 1 -> 1.0)
                int versionNumber;
                if (Int32.TryParse(versionString, out versionNumber) && versionNumber > 0)
                {
                    version = new SemanticVersion(new Version(versionNumber, 0));
                }
            }
            return version != null;
        }

        internal static SemanticVersion GetAssemblyVersion(string path)
        {
#if DNX451
            return new SemanticVersion(AssemblyName.GetAssemblyName(path).Version);
#else
            return new SemanticVersion(System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path).Version);
#endif
        }

        private static IDictionary<string, string> PopulateKnownFrameworks()
        {
            var frameworks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { DnxFrameworkShortName, DnxFrameworkIdentifier },
                { DnxCoreFrameworkShortName, DnxCoreFrameworkIdentifier },
                { "aspnet", AspNetFrameworkIdentifier },
                { "asp.net", AspNetFrameworkIdentifier },
                { "aspnetcore", AspNetCoreFrameworkIdentifier },
                { "asp.netcore", AspNetCoreFrameworkIdentifier },
                { NetPlatformFrameworkShortName, NetPlatformFrameworkIdentifier },
                { NetPlatformFrameworkIdentifier, NetPlatformFrameworkIdentifier },

                { "NET", NetFrameworkIdentifier },

                { ".NET", NetFrameworkIdentifier },
                { "NETFramework", NetFrameworkIdentifier },
                { ".NETFramework", NetFrameworkIdentifier },
                { "NETCore", NetCoreFrameworkIdentifier},
                { ".NETCore", NetCoreFrameworkIdentifier},
                { "WinRT", NetCoreFrameworkIdentifier},     // 'WinRT' is now deprecated. Use 'Windows' or 'win' instead.
                { ".NETMicroFramework", ".NETMicroFramework" },
                { "netmf", ".NETMicroFramework" },
                { "SL", "Silverlight" },
                { "Silverlight", "Silverlight" },
                { ".NETPortable", PortableFrameworkIdentifier },
                { "NETPortable", PortableFrameworkIdentifier },
                { "portable", PortableFrameworkIdentifier },
                { "wp", "WindowsPhone" },
                { "WindowsPhone", "WindowsPhone" },
                { "Windows", "Windows" },
                { "win", "Windows" },
                { "MonoAndroid", "MonoAndroid" },
                { "MonoTouch", "MonoTouch" },
                { "MonoMac", "MonoMac" },
                { "native", "native"},
                { "WindowsPhoneApp", "WindowsPhoneApp"},
                { "wpa", "WindowsPhoneApp"},
                { "k", "K" }
            };

            return frameworks;
        }
    }
}
