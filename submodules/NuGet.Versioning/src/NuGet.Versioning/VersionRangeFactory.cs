using System;
using System.Globalization;
using System.Linq;

namespace NuGet.Versioning
{
    /// <summary>
    /// Static factory methods for creating version range objects.
    /// </summary>
    public partial class VersionRange
    {
        /// <summary>
        /// The version string is either a simple version or an arithmetic range
        /// e.g.
        ///      1.0         --> 1.0 ≤ x
        ///      (,1.0]      --> x ≤ 1.0
        ///      (,1.0)      --> x &lt; 1.0
        ///      [1.0]       --> x == 1.0
        ///      (1.0,)      --> 1.0 &lt; x
        ///      (1.0, 2.0)   --> 1.0 &lt; x &lt; 2.0
        ///      [1.0, 2.0]   --> 1.0 ≤ x ≤ 2.0
        /// </summary>
        public static VersionRange Parse(string value)
        {
            VersionRange versionInfo;
            if (!TryParse(value, out versionInfo))
            {
                throw new ArgumentException(
                    String.Format(CultureInfo.CurrentCulture,
                     Resources.Invalidvalue, value));
            }

            return versionInfo;
        }

        /// <summary>
        /// Parses a VersionRange from its string representation.
        /// </summary>
        public static bool TryParse(string value, out VersionRange versionRange)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            value = value.Trim();

            // First, try to parse it as a plain version string
            NuGetVersion version;
            if (NuGetVersion.TryParse(value, out version))
            {
                // A plain version is treated as an inclusive minimum range
                versionRange = new VersionRange(version);

                return true;
            }

            // It's not a plain version, so it must be using the bracket arithmetic range syntax

            versionRange = null;

            // Fail early if the string is too short to be valid
            if (value.Length < 3)
            {
                return false;
            }

            bool isMinInclusive, isMaxInclusive;
            NuGetVersion minVersion = null, maxVersion = null;

            // The first character must be [ to (
            switch (value.Substring(0, 1))
            {
                case "[":
                    isMinInclusive = true;
                    break;
                case "(":
                    isMinInclusive = false;
                    break;
                default:
                    return false;
            }

            // The last character must be ] ot )
            switch (value.Substring(value.Length-1, 1))
            {
                case "]":
                    isMaxInclusive = true;
                    break;
                case ")":
                    isMaxInclusive = false;
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
            string minvalue = parts[0];
            string maxvalue = (parts.Length == 2) ? parts[1] : parts[0];

            // Only parse the min version if it's non-empty
            if (!String.IsNullOrWhiteSpace(minvalue))
            {
                if (!NuGetVersion.TryParse(minvalue, out version))
                {
                    return false;
                }
                minVersion = version;
            }

            // Same deal for max
            if (!String.IsNullOrWhiteSpace(maxvalue))
            {
                if (!NuGetVersion.TryParse(maxvalue, out version))
                {
                    return false;
                }
                maxVersion = version;
            }

            // Successful parse!
            versionRange = new VersionRange(minVersion, isMinInclusive, maxVersion, isMaxInclusive);
            return true;
        }
    }
}
