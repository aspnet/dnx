using System;
using NuGet.Versioning;

namespace NuGet.LibraryModel
{
    public class LibraryRange : IEquatable<LibraryRange>
    {
        public string Name { get; }

        public NuGetVersionRange VersionRange { get; }

        public string Type { get; }

        public LibraryRange(string name, string type, NuGetVersionRange versionRange)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if(string.IsNullOrEmpty(type))
            {
                throw new ArgumentNullException(type);
            }

            Name = name;
            VersionRange = versionRange;
            Type = type;
        }

        public override string ToString()
        {
            return Name + " " + (VersionRange?.ToString());
        }

        public bool Equals(LibraryRange other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name) &&
                Equals(VersionRange, other.VersionRange) &&
                Equals(Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LibraryRange)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^
                    (VersionRange != null ? VersionRange.GetHashCode() : 0) ^
                    (Type != null ? Type.GetHashCode() : 0);
            }
        }

        public static bool operator ==(LibraryRange left, LibraryRange right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LibraryRange left, LibraryRange right)
        {
            return !Equals(left, right);
        }
    }
}