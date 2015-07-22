using System;
using System.Text;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class SemanticVersionRange : IEquatable<SemanticVersionRange>
    {
        public SemanticVersionRange()
        {
        }

        public SemanticVersionRange(IVersionSpec versionSpec)
        {
            MinVersion = versionSpec.MinVersion;
            MaxVersion = versionSpec.MaxVersion;
            VersionFloatBehavior = SemanticVersionFloatBehavior.None;
            IsMaxInclusive = versionSpec.IsMaxInclusive;
        }

        public SemanticVersionRange(SemanticVersion version)
        {
            MinVersion = version;
            VersionFloatBehavior = SemanticVersionFloatBehavior.None;
        }

        public SemanticVersion MinVersion { get; set; }
        public SemanticVersion MaxVersion { get; set; }
        public SemanticVersionFloatBehavior VersionFloatBehavior { get; set; }
        public bool IsMaxInclusive { get; set; }

        public override string ToString()
        {
            if (MinVersion == MaxVersion && 
                VersionFloatBehavior == SemanticVersionFloatBehavior.None)
            {
                return MinVersion.ToString();
            }

            var sb = new StringBuilder();
            sb.Append(">= ");
            switch (VersionFloatBehavior)
            {
                case SemanticVersionFloatBehavior.None:
                    sb.Append(MinVersion);
                    break;
                case SemanticVersionFloatBehavior.Prerelease:
                    sb.AppendFormat("{0}-*", MinVersion);
                    break;
                case SemanticVersionFloatBehavior.Revision:
                    sb.AppendFormat("{0}.{1}.{2}.*",
                        MinVersion.Version.Major,
                        MinVersion.Version.Minor,
                        MinVersion.Version.Build);
                    break;
                case SemanticVersionFloatBehavior.Build:
                    sb.AppendFormat("{0}.{1}.*",
                        MinVersion.Version.Major,
                        MinVersion.Version.Minor);
                    break;
                case SemanticVersionFloatBehavior.Minor:
                    sb.AppendFormat("{0}.{1}.*",
                        MinVersion.Version.Major);
                    break;
                case SemanticVersionFloatBehavior.Major:
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

        public bool Equals(SemanticVersionRange other)
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
            return Equals((SemanticVersionRange)obj);
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
    }
}