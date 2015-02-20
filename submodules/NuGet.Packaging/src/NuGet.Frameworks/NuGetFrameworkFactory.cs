using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Frameworks
{
    public partial class NuGetFramework
    {
        /// <summary>
        /// An unknown or invalid framework
        /// </summary>
        public static readonly NuGetFramework UnsupportedFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Unsupported);

        /// <summary>
        /// A framework with no specific target framework. This can be used for content only packages.
        /// </summary>
        public static readonly NuGetFramework AgnosticFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Agnostic);

        /// <summary>
        /// A wildcard matching all frameworks
        /// </summary>
        public static readonly NuGetFramework AnyFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any);

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the default mappings.
        /// </summary>
        public static NuGetFramework Parse(string folderName)
        {
            return Parse(folderName, DefaultFrameworkNameProvider.Instance);
        }

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the given mappings.
        /// </summary>
        public static NuGetFramework Parse(string folderName, IFrameworkNameProvider mappings)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException("folderName");
            }

            NuGetFramework framework = null;

            if (folderName.IndexOf(',') > -1)
            {
                framework = ParseFrameworkName(folderName, mappings);
            }
            else
            {
                framework = ParseFolder(folderName, mappings);
            }

            return framework;
        }

        /// <summary>
        /// Creates a NuGetFramework from a .NET FrameworkName
        /// </summary>
        public static NuGetFramework ParseFrameworkName(string frameworkName, IFrameworkNameProvider mappings)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException("frameworkName");
            }

            string[] parts = frameworkName.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

            NuGetFramework result = null;

            // if the first part is a special framework, ignore the rest
            if (!TryParseSpecialFramework(parts[0], out result))
            {
                string platform = null;
                if (!mappings.TryGetIdentifier(parts[0], out platform))
                {
                    platform = parts[0];
                }

                Version version = new Version(0, 0);
                string profile = null;

                string versionPart = parts.Where(s => s.IndexOf("Version=", StringComparison.OrdinalIgnoreCase) == 0).SingleOrDefault();
                string profilePart = parts.Where(s => s.IndexOf("Profile=", StringComparison.OrdinalIgnoreCase) == 0).SingleOrDefault();

                if (!String.IsNullOrEmpty(versionPart))
                {
                    Version.TryParse(versionPart.Split('=')[1].TrimStart('v'), out version);
                }

                if (!String.IsNullOrEmpty(profilePart))
                {
                    profile = profilePart.Split('=')[1];
                }

                result = new NuGetFramework(platform, version, profile);
            }

            return result;
        }

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the given mappings.
        /// </summary>
        public static NuGetFramework ParseFolder(string folderName, IFrameworkNameProvider mappings)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException("folderName");
            }

            if (folderName.IndexOf('%') > -1)
            {
                folderName = Uri.UnescapeDataString(folderName);
            }

            NuGetFramework result = null;

            // first check if we have a special or common framework
            if (!TryParseSpecialFramework(folderName, out result) && !TryParseCommonFramework(folderName, out result))
            {
                // assume this is unsupported unless we find a match
                result = UnsupportedFramework;

                Tuple<string, string, string> parts = RawParse(folderName);

                if (parts != null)
                {
                    string framework = null;

                    // TODO: support number only folder names like 45
                    if (mappings.TryGetIdentifier(parts.Item1, out framework))
                    {
                        Version version = FrameworkConstants.EmptyVersion;

                        if (parts.Item2 == null || mappings.TryGetVersion(parts.Item2, out version))
                        {
                            string profileShort = parts.Item3;
                            string profile = null;
                            if (!mappings.TryGetProfile(framework, profileShort, out profile))
                            {
                                profile = profileShort ?? string.Empty;
                            }

                            if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Portable, framework))
                            {
                                IEnumerable<NuGetFramework> clientFrameworks = null;
                                mappings.TryGetPortableFrameworks(profileShort, out clientFrameworks);

                                int profileNumber = -1;
                                if (mappings.TryGetPortableProfile(clientFrameworks, out profileNumber))
                                {
                                    string portableProfileNumber = FrameworkNameHelpers.GetPortableProfileNumberString(profileNumber);
                                    result = new NuGetFramework(framework, version, portableProfileNumber);
                                }
                                else
                                {
                                    // TODO: should this be unsupported?
                                    result = new NuGetFramework(framework, version, profileShort);
                                }
                            }
                            else
                            {
                                result = new NuGetFramework(framework, version, profile);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static Tuple<string, string, string> RawParse(string s)
        {
            string identifier = string.Empty;
            string profile = string.Empty;
            string version = null;

            char[] chars = s.ToCharArray();

            int versionStart = 0;

            while (versionStart < chars.Length && IsLetterOrDot(chars[versionStart]))
            {
                versionStart++;
            }

            if (versionStart > 0)
            {
                identifier = s.Substring(0, versionStart);
            }
            else
            {
                // invalid, we no longer support names like: 40
                return null;
            }

            int profileStart = versionStart;

            while (profileStart < chars.Length && IsDigitOrDot(chars[profileStart]))
            {
                profileStart++;
            }

            int versionLength = profileStart - versionStart;

            if (versionLength > 0)
            {
                version = s.Substring(versionStart, versionLength);
            }

            if (profileStart < chars.Length)
            {
                if (chars[profileStart] == '-')
                {
                    int actualProfileStart = profileStart + 1;

                    if (actualProfileStart == chars.Length)
                    {
                        // empty profiles are not allowed
                        return null;
                    }

                    profile = s.Substring(actualProfileStart, s.Length - actualProfileStart);

                    foreach (char c in profile.ToArray())
                    {
                        // validate the profile string to AZaz09-+.
                        if (!IsValidProfileChar(c))
                        {
                            return null;
                        }
                    }
                }
                else
                {
                    // invalid profile
                    return null;
                }
            }

            return new Tuple<string, string, string>(identifier, version, profile);
        }

        private static bool IsLetterOrDot(char c)
        {
            int x = (int)c;

            // "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
            return (x >= 65 && x <= 90) || (x >= 97 && x <= 122) || x == 46;
        }

        private static bool IsDigitOrDot(char c)
        {
            int x = (int)c;

            // "0123456789"
            return (x >= 48 && x <= 57) || x == 46;
        }

        private static bool IsValidProfileChar(char c)
        {
            int x = (int)c;

            // letter, digit, dot, dash, or plus
            return (x >= 48 && x <= 57) || (x >= 65 && x <= 90) || (x >= 97 && x <= 122) || x == 46 || x == 43 || x == 45;
        }

        private static bool TryParseSpecialFramework(string frameworkString, out NuGetFramework framework)
        {
            framework = null;

            if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Any))
            {
                framework = NuGetFramework.AnyFramework;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Agnostic))
            {
                framework = NuGetFramework.AgnosticFramework;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Unsupported))
            {
                framework = NuGetFramework.UnsupportedFramework;
            }

            return framework != null;
        }

        /// <summary>
        /// A set of special and common frameworks that can be returned from the list of constants without parsing
        /// Using the interned frameworks here optimizes comparisons since they can be checked by reference.
        /// This is designed to optimize 
        /// </summary>
        private static bool TryParseCommonFramework(string frameworkString, out NuGetFramework framework)
        {
            framework = null;

            if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, "aspnet") || StringComparer.OrdinalIgnoreCase.Equals(frameworkString, "aspnet50"))
            {
                framework = FrameworkConstants.CommonFrameworks.AspNet50;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, "aspnetcore") || StringComparer.OrdinalIgnoreCase.Equals(frameworkString, "aspnetcore50"))
            {
                framework = FrameworkConstants.CommonFrameworks.AspNetCore50;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, "net40"))
            {
                framework = FrameworkConstants.CommonFrameworks.Net4;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, "net45"))
            {
                framework = FrameworkConstants.CommonFrameworks.Net45;
            }

            return framework != null;
        }
    }
}
