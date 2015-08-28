// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Resources;

namespace NuGet
{
    /// <summary>
    /// A hybrid implementation of SemVer that supports semantic versioning as described at http://semver.org while not strictly enforcing it to 
    /// allow older 4-digit versioning schemes to continue wokring.
    /// </summary>
    public sealed class SemanticVersion : IComparable, IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        public static Func<string, SemanticVersion> Factory { get; set; } = version => Parse(version);

        public static SemanticVersion Create(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentNullException(nameof(version));
            }

            return Factory(version);
        }

        public static SemanticVersion Create(Version version)
        {
            return Create(version, string.Empty);
        }

        public static SemanticVersion Create(Version version, string specialVersion)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            var normalizedVersion = NormalizeVersionValue(version);
            var key = version.ToString();
            if (!string.IsNullOrEmpty(specialVersion))
            {
                key += "-" + specialVersion;
            }

            return Factory(key);
        }

        public static bool TryCreate(string version, out SemanticVersion result)
        {
            try
            {
                result = Factory(version);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        private SemanticVersion()
        {
        }

        /// <summary>
        /// Gets the normalized version portion.
        /// </summary>
        public Version Version { get; private set; }

        /// <summary>
        /// Gets the optional special version.
        /// </summary>
        public string SpecialVersion { get; private set; }

        public string OriginalString { get; private set; }

        public string GetNormalizedVersionString()
        {
            var revision = Version.Revision > 0 ? ("." + Version.Revision.ToString(CultureInfo.InvariantCulture)) : string.Empty;
            var specialVer = !string.IsNullOrEmpty(SpecialVersion) ? ("-" + SpecialVersion) : string.Empty;

            // SemanticVersion normalizes the missing components to 0.
            return $"{Version.Major}.{Version.Minor}.{Version.Build}{revision}{specialVer}";
        }

        public string[] GetOriginalVersionComponents()
        {
            if (!string.IsNullOrEmpty(OriginalString))
            {
                string original;

                // search the start of the SpecialVersion part, if any
                int dashIndex = OriginalString.IndexOf('-');
                if (dashIndex != -1)
                {
                    // remove the SpecialVersion part
                    original = OriginalString.Substring(0, dashIndex);
                }
                else
                {
                    original = OriginalString;
                }

                return SplitAndPadVersionString(original);
            }
            else
            {
                return SplitAndPadVersionString(Version.ToString());
            }
        }

        private static string[] SplitAndPadVersionString(string version)
        {
            string[] a = version.Split('.');
            if (a.Length == 4)
            {
                return a;
            }
            else
            {
                // if 'a' has less than 4 elements, we pad the '0' at the end 
                // to make it 4.
                var b = new string[4] { "0", "0", "0", "0" };
                Array.Copy(a, 0, b, 0, a.Length);
                return b;
            }
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed by an optional special version.
        /// </summary>
        public static SemanticVersion Parse(string version)
        {
            SemanticVersion semVer;
            if (!TryParse(version, strict: false, result: out semVer))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, NuGetResources.InvalidVersionString, version), nameof(version));
            }

            return semVer;
        }

        /// <summary>
        /// Try parse a version string.
        /// 
        /// Under loose mode loose semantic versioning rule is used. It allows 2-4 version components followed by an optional special version.
        /// Under strict mode exactly 3 components are allowed with an optional special version.
        /// </summary>
        /// <param name="version">version in string</param>
        /// <param name="strict">true if run under strick mode</param>
        /// <param name="result">result in SemanticVersion</param>
        /// <returns></returns>
        public static bool TryParse(string version, bool strict, out SemanticVersion result)
        {
            result = null;
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            version = version.Trim();
            var versionPart = version;

            string specialVersion = string.Empty;
            if (version.IndexOf('-') != -1)
            {
                var parts = version.Split(new char[] { '-' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    return false;
                }

                versionPart = parts[0];
                specialVersion = parts[1];
            }

            Version versionValue;
            if (!Version.TryParse(versionPart, out versionValue))
            {
                return false;
            }

            if (strict)
            {
                // Must have major, minor and build only.
                if (versionValue.Major == -1 ||
                    versionValue.Minor == -1 ||
                    versionValue.Build == -1 ||
                    versionValue.Revision != -1)
                {
                    return false;
                }
            }

            result = new SemanticVersion
            {
                Version = NormalizeVersionValue(versionValue),
                SpecialVersion = specialVersion,
                OriginalString = version.Replace(" ", "")
            };

            return true;
        }

        private static Version NormalizeVersionValue(Version version)
        {
            return new Version(version.Major,
                               version.Minor,
                               Math.Max(version.Build, 0),
                               Math.Max(version.Revision, 0));
        }

        public int CompareTo(object obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return 1;
            }
            SemanticVersion other = obj as SemanticVersion;
            if (other == null)
            {
                throw new ArgumentException(NuGetResources.TypeMustBeASemanticVersion, nameof(obj));
            }
            return CompareTo(other);
        }

        public int CompareTo(SemanticVersion other)
        {
            if (Object.ReferenceEquals(other, null))
            {
                return 1;
            }

            int result = Version.CompareTo(other.Version);

            if (result != 0)
            {
                return result;
            }

            bool empty = string.IsNullOrEmpty(SpecialVersion);
            bool otherEmpty = string.IsNullOrEmpty(other.SpecialVersion);
            if (empty && otherEmpty)
            {
                return 0;
            }
            else if (empty)
            {
                return 1;
            }
            else if (otherEmpty)
            {
                return -1;
            }
            return StringComparer.OrdinalIgnoreCase.Compare(SpecialVersion, other.SpecialVersion);
        }

        public static bool operator ==(SemanticVersion version1, SemanticVersion version2)
        {
            if (Object.ReferenceEquals(version1, null))
            {
                return Object.ReferenceEquals(version2, null);
            }
            return version1.Equals(version2);
        }

        public static bool operator !=(SemanticVersion version1, SemanticVersion version2)
        {
            return !(version1 == version2);
        }

        public static bool operator <(SemanticVersion version1, SemanticVersion version2)
        {
            if (version1 == null)
            {
                throw new ArgumentNullException(nameof(version1));
            }
            return version1.CompareTo(version2) < 0;
        }

        public static bool operator <=(SemanticVersion version1, SemanticVersion version2)
        {
            return (version1 == version2) || (version1 < version2);
        }

        public static bool operator >(SemanticVersion version1, SemanticVersion version2)
        {
            if (version1 == null)
            {
                throw new ArgumentNullException(nameof(version1));
            }
            return version2 < version1;
        }

        public static bool operator >=(SemanticVersion version1, SemanticVersion version2)
        {
            return (version1 == version2) || (version1 > version2);
        }

        public override string ToString()
        {
            return OriginalString;
        }

        public bool Equals(SemanticVersion other)
        {
            return !Object.ReferenceEquals(null, other) &&
                   Version.Equals(other.Version) &&
                   SpecialVersion.Equals(other.SpecialVersion, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            SemanticVersion semVer = obj as SemanticVersion;
            return !Object.ReferenceEquals(null, semVer) && Equals(semVer);
        }

        public override int GetHashCode()
        {
            int hashCode = Version.GetHashCode();
            if (SpecialVersion != null)
            {
                hashCode = hashCode * 4567 + SpecialVersion.GetHashCode();
            }

            return hashCode;
        }
    }
}
