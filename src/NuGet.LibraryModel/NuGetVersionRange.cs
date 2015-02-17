using System;
using System.Text;

namespace NuGet.Versioning
{
    public class NuGetVersionRange : IEquatable<NuGetVersionRange>
    {
        public NuGetVersionRange()
        {
        }

        public NuGetVersionRange(VersionRange versionRange)
        {
            MinVersion = versionRange.MinVersion as NuGetVersion;
            MaxVersion = versionRange.MaxVersion as NuGetVersion;
            IsMaxInclusive = versionRange.IsMaxInclusive;
            VersionFloatBehavior = NuGetVersionFloatBehavior.None;
        }

        public NuGetVersionRange(NuGetVersion version)
        {
            MinVersion = version;
            VersionFloatBehavior = NuGetVersionFloatBehavior.None;
        }

        public NuGetVersion MinVersion { get; set; }
        public NuGetVersion MaxVersion { get; set; }
        public NuGetVersionFloatBehavior VersionFloatBehavior { get; set; }
        public bool IsMaxInclusive { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(">= ");
            switch (VersionFloatBehavior)
            {
                case NuGetVersionFloatBehavior.None:
                    sb.Append(MinVersion);
                    break;
                case NuGetVersionFloatBehavior.Prerelease:
                    sb.AppendFormat("{0}-*", MinVersion);
                    break;
                case NuGetVersionFloatBehavior.Revision:
                    sb.AppendFormat("{0}.{1}.{2}.*",
                        MinVersion.Version.Major,
                        MinVersion.Version.Minor,
                        MinVersion.Version.Build);
                    break;
                case NuGetVersionFloatBehavior.Build:
                    sb.AppendFormat("{0}.{1}.*",
                        MinVersion.Version.Major,
                        MinVersion.Version.Minor);
                    break;
                case NuGetVersionFloatBehavior.Minor:
                    sb.AppendFormat("{0}.{1}.*",
                        MinVersion.Version.Major);
                    break;
                case NuGetVersionFloatBehavior.Major:
                    sb.AppendFormat("*");
                    break;
                default:
                    break;
            }

            if (MaxVersion != null)
            {
                sb.Append(IsMaxInclusive ? " <= " : " < ");
                sb.Append(MaxVersion);
            }

            return sb.ToString();
        }

        public bool Equals(NuGetVersionRange other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(MinVersion, other.MinVersion) &&
                Equals(MaxVersion, other.MaxVersion) &&
                Equals(VersionFloatBehavior, other.VersionFloatBehavior) &&
                Equals(IsMaxInclusive, other.IsMaxInclusive);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((NuGetVersionRange)obj);
        }

        public bool EqualsFloating(NuGetVersion version)
        {
            switch (VersionFloatBehavior)
            {
                case NuGetVersionFloatBehavior.Prerelease:
                    return MinVersion.Version == version.Version &&
                           version.Release.StartsWith(MinVersion.Release, StringComparison.OrdinalIgnoreCase);

                case NuGetVersionFloatBehavior.Revision:
                    return MinVersion.Version.Major == version.Version.Major &&
                           MinVersion.Version.Minor == version.Version.Minor &&
                           MinVersion.Version.Build == version.Version.Build &&
                           MinVersion.Version.Revision == version.Version.Revision;

                case NuGetVersionFloatBehavior.Build:
                    return MinVersion.Version.Major == version.Version.Major &&
                           MinVersion.Version.Minor == version.Version.Minor &&
                           MinVersion.Version.Build == version.Version.Build;

                case NuGetVersionFloatBehavior.Minor:
                    return MinVersion.Version.Major == version.Version.Major &&
                           MinVersion.Version.Minor == version.Version.Minor;

                case NuGetVersionFloatBehavior.Major:
                    return MinVersion.Version.Major == version.Version.Major;

                case NuGetVersionFloatBehavior.None:
                    return MinVersion == version;
                default:
                    return false;
            }
        }

        public override int GetHashCode()
        {
            int hashCode = MinVersion.GetHashCode();

            hashCode = CombineHashCode(hashCode, VersionFloatBehavior.GetHashCode());

            if (MaxVersion != null)
            {
                hashCode = CombineHashCode(hashCode, MaxVersion.GetHashCode());
            }

            hashCode = CombineHashCode(hashCode, IsMaxInclusive.GetHashCode());

            return hashCode;
        }

        private static int CombineHashCode(int h1, int h2)
        {
            return h1 * 4567 + h2;
        }

        public static NuGetVersionRange Parse(string value)
        {
            var floatBehavior = NuGetVersionFloatBehavior.None;

            // Support snapshot versions
            if (value.EndsWith("-*"))
            {
                floatBehavior = NuGetVersionFloatBehavior.Prerelease;
                value = value.Substring(0, value.Length - 2);
            }

            var range = VersionRange.Parse(value);

            return new NuGetVersionRange(range)
            {
                VersionFloatBehavior = floatBehavior
            };
        }
    }
}