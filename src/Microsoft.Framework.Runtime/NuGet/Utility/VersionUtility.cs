// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Framework.Runtime;
using NuGet.Resources;
using CompatibilityMapping = System.Collections.Generic.Dictionary<string, string[]>;
using Microsoft.Framework.Runtime.Common.Impl;

namespace NuGet
{
    public static class VersionUtility
    {
        public static readonly string AspNetCoreFrameworkIdentifier = "Asp.NetCore";
        public static readonly string DnxCoreFrameworkIdentifier = "DNXCore";
        public static readonly string CoreFrameworkIdentifier = "Core";

        internal const string NetFrameworkIdentifier = ".NETFramework";
        private const string NetCoreFrameworkIdentifier = ".NETCore";
        private const string PortableFrameworkIdentifier = ".NETPortable";
        internal const string AspNetFrameworkIdentifier = "Asp.Net";
        internal const string DnxFrameworkIdentifier = "DNX";
        internal const string DnxFrameworkShortName = "dnx";
        internal const string DnxCoreFrameworkShortName = "dnxcore";

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

        private static readonly Dictionary<string, CompatibilityMapping> _compatibiltyMapping = new Dictionary<string, CompatibilityMapping>(StringComparer.OrdinalIgnoreCase) {
            {
                // Client profile is compatible with the full framework (empty string is full)
                NetFrameworkIdentifier, new CompatibilityMapping(StringComparer.OrdinalIgnoreCase) {
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

        private static readonly Version MaxVersion = new Version(Int32.MaxValue, Int32.MaxValue, Int32.MaxValue, Int32.MaxValue);

        private static readonly Dictionary<string, FrameworkName> _equivalentProjectFrameworks = new Dictionary<string, FrameworkName>()
        {
            // Allow an aspnetcore package to be installed in a dnxcore project 
            { DnxCoreFrameworkIdentifier, new FrameworkName(AspNetCoreFrameworkIdentifier, MaxVersion) },

            // Allow an aspnet package to be installed in a dnx project
            { DnxFrameworkIdentifier, new FrameworkName(AspNetFrameworkIdentifier, MaxVersion) },

            // Allow a net package to be installed in an aspnet (or dnx, transitively by above) project
            { AspNetFrameworkIdentifier, new FrameworkName(NetFrameworkIdentifier, MaxVersion) }
        };

        public static Version DefaultTargetFrameworkVersion
        {
            get
            {
                // We need to parse the version name out from the mscorlib's assembly name since
                // we can't call GetName() in medium trust
                return typeof(string).GetTypeInfo().Assembly.GetName().Version;
            }
        }

        public static FrameworkName DefaultTargetFramework
        {
            get
            {
                return new FrameworkName(NetFrameworkIdentifier, DefaultTargetFrameworkVersion);
            }
        }

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
                throw new ArgumentNullException("frameworkName");
            }

            // {Identifier}{Version}-{Profile}

            // Split the framework name into 3 parts, identifier, version and profile.
            string identifierPart = null;
            string versionPart = null;

            string[] parts = frameworkName.Split('-');

            if (parts.Length > 2)
            {
                throw new ArgumentException(NuGetResources.InvalidFrameworkNameFormat, "frameworkName");
            }

            string frameworkNameAndVersion = parts.Length > 0 ? parts[0].Trim() : null;
            string profilePart = parts.Length > 1 ? parts[1].Trim() : null;

            if (String.IsNullOrEmpty(frameworkNameAndVersion))
            {
                throw new ArgumentException(NuGetResources.MissingFrameworkName, "frameworkName");
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

                version = _emptyVersion;
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

        internal static void ValidatePortableFrameworkProfilePart(string profilePart)
        {
            if (String.IsNullOrEmpty(profilePart))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileEmpty, "profilePart");
            }

            if (profilePart.Contains('-'))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileHasDash, "profilePart");
            }

            if (profilePart.Contains(' '))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileHasSpace, "profilePart");
            }

            string[] parts = profilePart.Split('+');
            if (parts.Any(p => String.IsNullOrEmpty(p)))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileComponentIsEmpty, "profilePart");
            }

            // Prevent portable framework inside a portable framework - Inception
            if (parts.Any(p => p.StartsWith("portable", StringComparison.OrdinalIgnoreCase)) ||
                parts.Any(p => p.StartsWith("NETPortable", StringComparison.OrdinalIgnoreCase)) ||
                parts.Any(p => p.StartsWith(".NETPortable", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(NuGetResources.PortableFrameworkProfileComponentIsPortable, "profilePart");
            }
        }

        /// <summary>
        /// Trims trailing zeros in revision and build.
        /// </summary>
        public static Version TrimVersion(Version version)
        {
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            if (version.Build == 0 && version.Revision == 0)
            {
                version = new Version(version.Major, version.Minor);
            }
            else if (version.Revision == 0)
            {
                version = new Version(version.Major, version.Minor, version.Build);
            }

            return version;
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

        public static bool TryParseVersionSpec(string value, out IVersionSpec result)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
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

        /// <summary>
        /// The safe range is defined as the highest build and revision for a given major and minor version
        /// </summary>
        public static IVersionSpec GetSafeRange(SemanticVersion version)
        {
            return new VersionSpec
            {
                IsMinInclusive = true,
                MinVersion = version,
                MaxVersion = new SemanticVersion(new Version(version.Version.Major, version.Version.Minor + 1))
            };
        }

        public static string PrettyPrint(IVersionSpec versionSpec)
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

            string name;
            if (!_identifierToFrameworkFolder.TryGetValue(frameworkName.Identifier, out name))
            {
                name = frameworkName.Identifier;
            }

            // for Portable framework name, the short name has the form "portable-sl4+wp7+net45"
            string profile;
            if (name.Equals("portable", StringComparison.OrdinalIgnoreCase))
            {
                NetPortableProfile portableProfile = NetPortableProfile.Parse(frameworkName.Profile);
                if (portableProfile != null)
                {
                    profile = portableProfile.CustomProfileString;
                }
                else
                {
                    profile = frameworkName.Profile;
                }
            }
            else
            {
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

        public static FrameworkName ParseFrameworkFolderName(string path)
        {
            string effectivePath;
            return ParseFrameworkFolderName(path, strictParsing: true, effectivePath: out effectivePath);
        }

        /// <summary>
        /// Parses the specified string into FrameworkName object.
        /// </summary>
        /// <param name="path">The string to be parse.</param>
        /// <param name="strictParsing">if set to <c>true</c>, parse the first folder of path even if it is unrecognized framework.</param>
        /// <param name="effectivePath">returns the path after the parsed target framework</param>
        /// <returns></returns>
        public static FrameworkName ParseFrameworkFolderName(string path, bool strictParsing, out string effectivePath)
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

        public static bool TryGetCompatibleItems<T>(FrameworkName projectFramework, IEnumerable<T> items, out IEnumerable<T> compatibleItems) where T : IFrameworkTargetable
        {
            if (!items.Any())
            {
                compatibleItems = Enumerable.Empty<T>();
                return true;
            }

            // Not all projects have a framework, we need to consider those projects.
            var internalProjectFramework = projectFramework ?? EmptyFramework;

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

        internal static Version NormalizeVersion(Version verison)
        {
            return new Version(verison.Major,
                               verison.Minor,
                               Math.Max(verison.Build, 0),
                               Math.Max(verison.Revision, 0));
        }

        internal static FrameworkName NormalizeFrameworkName(FrameworkName framework)
        {
            FrameworkName aliasFramework;
            if (_frameworkNameAlias.TryGetValue(framework, out aliasFramework))
            {
                return aliasFramework;
            }

            return framework;
        }

        /// <summary>
        /// Returns all possible versions for a version. i.e. 1.0 would return 1.0, 1.0.0, 1.0.0.0
        /// </summary>
        internal static IEnumerable<SemanticVersion> GetPossibleVersions(SemanticVersion semVer)
        {
            // Trim the version so things like 1.0.0.0 end up being 1.0
            Version version = TrimVersion(semVer.Version);

            yield return new SemanticVersion(version, semVer.SpecialVersion);

            if (version.Build == -1 && version.Revision == -1)
            {
                yield return new SemanticVersion(new Version(version.Major, version.Minor, 0), semVer.SpecialVersion);
                yield return new SemanticVersion(new Version(version.Major, version.Minor, 0, 0), semVer.SpecialVersion);
            }
            else if (version.Revision == -1)
            {
                yield return new SemanticVersion(new Version(version.Major, version.Minor, version.Build, 0), semVer.SpecialVersion);
            }
        }

        public static bool IsCompatible(FrameworkName frameworkName, IEnumerable<FrameworkName> supportedFrameworks)
        {
            if (supportedFrameworks.Any())
            {
                return supportedFrameworks.Any(supportedFramework => IsCompatible(frameworkName, supportedFramework));
            }

            // No supported frameworks means that everything is supported.
            return true;
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

            targetFrameworkName = NormalizeFrameworkName(targetFrameworkName);
            frameworkName = NormalizeFrameworkName(frameworkName);

        check:

            if (!frameworkName.Identifier.Equals(targetFrameworkName.Identifier, StringComparison.OrdinalIgnoreCase))
            {
                // Try to convert the project framework into an equivalent target framework
                // If the identifiers didn't match, we need to see if this framework has an equivalent framework that DOES match.
                // If it does, we use that from here on.
                // Example:
                //  If the Project Targets ASP.Net, Version=5.0. It can accept Packages targetting .NETFramework, Version=4.5.1
                //  so since the identifiers don't match, we need to "translate" the project target framework to .NETFramework
                //  however, we still want direct ASP.Net == ASP.Net matches, so we do this ONLY if the identifiers don't already match

                if (_equivalentProjectFrameworks.TryGetValue(frameworkName.Identifier, out frameworkName))
                {
                    // Goto might be evil but it's so nice to use here
                    goto check;
                }
                else
                {
                    return false;
                }
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
                    if (string.Equals(frameworkName.Identifier, AspNetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(frameworkName.Identifier, DnxCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        var frameworkIdentifierLookup = targetFrameworkPortableProfile.SupportedFrameworks
                                                                                      .Select(NormalizeFrameworkName)
                                                                                      .ToLookup(f => f.Identifier);

                        if (frameworkIdentifierLookup[NetFrameworkIdentifier].Any(f => f.Version >= new Version(4, 5)) &&
                            frameworkIdentifierLookup[NetCoreFrameworkIdentifier].Any(f => f.Version >= new Version(4, 5)))
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
            frameworkName = NormalizeFrameworkName(frameworkName);
            targetFrameworkName = NormalizeFrameworkName(targetFrameworkName);

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
                    var compatibleFramework = profile.SupportedFrameworks.FirstOrDefault(f => VersionUtility.IsCompatible(projectFramework, f));
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
            // Algorithms: Give a score from 0 to N indicating how close *in version* each package platform is the projectâ€™s platforms 
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

        public static bool IsPortableFramework(this FrameworkName framework)
        {
            // The profile part has been verified in the ParseFrameworkName() method. 
            // By the time it is called here, it's guaranteed to be valid.
            // Thus we can ignore the profile part here
            return framework != null && PortableFrameworkIdentifier.Equals(framework.Identifier, StringComparison.OrdinalIgnoreCase);
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
                { "core", CoreFrameworkIdentifier },

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
