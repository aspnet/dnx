using System;

namespace Microsoft.Framework.Runtime
{
    public class LibraryRange : IEquatable<LibraryRange>
    {
        public string Name { get; set; }

        public SemanticVersionRange VersionRange { get; set; }

        public bool IsGacOrFrameworkReference { get; set; }

        // Information for the editor
        public string FileName { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }

        public override string ToString()
        {
            var name = IsGacOrFrameworkReference ? "framework/" + Name : Name;
            return name + " " + (VersionRange?.ToString());
        }

        public bool Equals(LibraryRange other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name) &&
                Equals(VersionRange, other.VersionRange) &&
                Equals(IsGacOrFrameworkReference, other.IsGacOrFrameworkReference);
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
                    (IsGacOrFrameworkReference.GetHashCode());
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