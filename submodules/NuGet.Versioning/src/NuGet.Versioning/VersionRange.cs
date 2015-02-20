using System;

namespace NuGet.Versioning
{
    /// <summary>
    /// Represents a range of versions.
    /// </summary>
    public partial class VersionRange : IFormattable, IEquatable<VersionRange>
    {
        private readonly bool _includeMinVersion;
        private readonly bool _includeMaxVersion;
        private readonly SimpleVersion _minVersion;
        private readonly SimpleVersion _maxVersion;
        private readonly bool _includePrerelease;

        /// <summary>
        /// Creates a VersionRange with the given min and max.
        /// </summary>
        /// <param name="minVersion">Lower bound of the version range.</param>
        /// <param name="includeMinVersion">True if minVersion satisfies the condition.</param>
        /// <param name="maxVersion">Upper bound of the version range.</param>
        /// <param name="includeMaxVersion">True if maxVersion satisfies the condition.</param>
        /// <param name="includePrerelease">True if prerelease versions should satisfy the condition.</param>
        public VersionRange(SimpleVersion minVersion=null, bool includeMinVersion=true, SimpleVersion maxVersion=null, 
            bool includeMaxVersion=false, bool? includePrerelease=null)
        {
            _minVersion = minVersion;
            _maxVersion = maxVersion;
            _includeMinVersion = includeMinVersion;
            _includeMaxVersion = includeMaxVersion;

            if (includePrerelease == null)
            {
                _includePrerelease = (_maxVersion != null && IsPrerelease(_maxVersion) == true) || 
                    (_minVersion != null && IsPrerelease(_minVersion) == true);
            }
            else
            {
                _includePrerelease = includePrerelease == true;
            }
        }

        /// <summary>
        /// True if MinVersion exists;
        /// </summary>
        public bool HasLowerBound
        {
            get
            {
                return _minVersion != null;
            }
        }

        /// <summary>
        /// True if MaxVersion exists.
        /// </summary>
        public bool HasUpperBound
        {
            get
            {
                return _maxVersion != null;
            }
        }

        /// <summary>
        /// True if both MinVersion and MaxVersion exist.
        /// </summary>
        public bool HasLowerAndUpperBounds
        {
            get
            {
                return HasLowerBound && HasUpperBound;
            }
        }

        /// <summary>
        /// True if MinVersion exists and is included in the range.
        /// </summary>
        public bool IsMinInclusive
        {
            get
            {
                return HasLowerBound && _includeMinVersion;
            }
        }

        /// <summary>
        /// True if MaxVersion exists and is included in the range.
        /// </summary>
        public bool IsMaxInclusive
        {
            get
            {
                return HasUpperBound && _includeMaxVersion;
            }
        }

        /// <summary>
        /// Maximum version allowed by this range.
        /// </summary>
        public SimpleVersion MaxVersion
        {
            get
            {
                return _maxVersion;
            }
        }

        /// <summary>
        /// Minimum version allowed by this range.
        /// </summary>
        public SimpleVersion MinVersion
        {
            get
            {
                return _minVersion;
            }
        }

        /// <summary>
        /// True if pre-release versions are included in this range.
        /// </summary>
        public bool IncludePrerelease
        {
            get
            {
                return _includePrerelease;
            }
        }

        /// <summary>
        /// Determines if an NuGetVersion meets the requirements.
        /// </summary>
        /// <param name="version">SemVer to compare</param>
        /// <returns>True if the given version meets the version requirements.</returns>
        public bool Satisfies(SimpleVersion version)
        {
            // ignore metadata by default when finding a range.
            return Satisfies(version, VersionComparer.VersionRelease);
        }

        /// <summary>
        /// Determines if an NuGetVersion meets the requirements using the given mode.
        /// </summary>
        /// <param name="version">SemVer to compare</param>
        /// <param name="versionComparison">VersionComparison mode used to determine the version range.</param>
        /// <returns>True if the given version meets the version requirements.</returns>
        public bool Satisfies(SimpleVersion version, VersionComparison versionComparison)
        {
            return Satisfies(version, new VersionComparer(versionComparison));
        }

        /// <summary>
        /// Determines if an NuGetVersion meets the requirements using the version comparer.
        /// </summary>
        /// <param name="version">SemVer to compare.</param>
        /// <param name="comparer">Version comparer used to determine if the version criteria is met.</param>
        /// <returns>True if the given version meets the version requirements.</returns>
        public bool Satisfies(SimpleVersion version, IVersionComparer comparer)
        {
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            // Determine if version is in the given range using the comparer.
            bool condition = true;
            if (HasLowerBound)
            {
                if (IsMinInclusive)
                {
                    condition &= comparer.Compare(MinVersion, version) <= 0;
                }
                else
                {
                    condition &= comparer.Compare(MinVersion, version) < 0;
                }
            }

            if (HasUpperBound)
            {
                if (IsMaxInclusive)
                {
                    condition &= comparer.Compare(MaxVersion, version) >= 0;
                }
                else
                {
                    condition &= comparer.Compare(MaxVersion, version) > 0;
                }
            }

            if (!IncludePrerelease)
            {
                condition &= IsPrerelease(version) != true;
            }

            return condition;
        }

        /// <summary>
        /// Normalized range string.
        /// </summary>
        public override string ToString()
        {
            return ToNormalizedString();
        }

        public virtual string ToNormalizedString()
        {
            return ToString("N", new VersionRangeFormatter());
        }

        /// <summary>
        /// Format the version range with an IFormatProvider
        /// </summary>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            string formattedString = null;

            if (formatProvider == null || !TryFormatter(format, formatProvider, out formattedString))
            {
                formattedString = ToString();
            }

            return formattedString;
        }

        /// <summary>
        /// Format the range
        /// </summary>
        protected bool TryFormatter(string format, IFormatProvider formatProvider, out string formattedString)
        {
            bool formatted = false;
            formattedString = null;

            if (formatProvider != null)
            {
                ICustomFormatter formatter = formatProvider.GetFormat(this.GetType()) as ICustomFormatter;
                if (formatter != null)
                {
                    formatted = true;
                    formattedString = formatter.Format(format, this, formatProvider);
                }
            }

            return formatted;
        }

        /// <summary>
        /// Format the version range in Pretty Print format.
        /// </summary>
        public string PrettyPrint()
        {
            return ToString("P", new VersionRangeFormatter());
        }

        /// <summary>
        /// Compares the object as a VersionRange with the default comparer
        /// </summary>
        public override bool Equals(object obj)
        {
            VersionRange range = obj as VersionRange;

            if (range != null)
            {
                return VersionRangeComparer.Default.Equals(this, range);
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code using the default comparer.
        /// </summary>
        public override int GetHashCode()
        {
            return VersionRangeComparer.Default.GetHashCode(this);
        }

        /// <summary>
        /// Default compare
        /// </summary>
        public bool Equals(VersionRange other)
        {
            return Equals(other, VersionRangeComparer.Default);
        }

        /// <summary>
        /// Use the VersionRangeComparer for equality checks
        /// </summary>
        public bool Equals(VersionRange other, IVersionRangeComparer comparer)
        {
            if (comparer == null)
            {
                throw new ArgumentNullException("comparer");
            }

            return comparer.Equals(this, other);
        }

        /// <summary>
        /// Use a specific VersionComparison for comparison
        /// </summary>
        public bool Equals(VersionRange other, VersionComparison versionComparison)
        {
            IVersionRangeComparer comparer = new VersionRangeComparer(versionComparison);
            return Equals(other, comparer);
        }

        /// <summary>
        ///  Use a specific IVersionComparer for comparison
        /// </summary>
        public bool Equals(VersionRange other, IVersionComparer versionComparer)
        {
            IVersionRangeComparer comparer = new VersionRangeComparer(versionComparer);
            return Equals(other, comparer);
        }

        /// <summary>
        /// A range that accepts all versions, prerelease and stable.
        /// </summary>
        public static VersionRange All
        {
            get
            {
                return new VersionRange(null, true, null, true, true);
            }
        }

        /// <summary>
        /// A range that accepts all stable versions
        /// </summary>
        public static VersionRange AllStable
        {
            get
            {
                return new VersionRange(null, true, null, true, false);
            }
        }

        /// <summary>
        /// A range that rejects all versions
        /// </summary>
        public static VersionRange None
        {
            get
            {
                return new VersionRange(new NuGetVersion(0, 0, 0), false, new NuGetVersion(0, 0, 0), false, false);
            }
        }

        private static bool? IsPrerelease(SimpleVersion version)
        {
            bool? b = null;

            SemanticVersion semVer = version as SemanticVersion;
            if (semVer != null)
            {
                b = semVer.IsPrerelease;
            }

            return b;
        }
    }
}