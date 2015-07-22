using System;
using System.Diagnostics;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryRange : IEquatable<LibraryRange>
    {
        private string _frameworkAssemblyName;

        public static readonly string FrameworkReferencePrefix = "fx/";

        public string Name { get; }

        public SemanticVersionRange VersionRange { get; set; }

        public bool IsGacOrFrameworkReference { get; }

        // Information for the editor
        public string FileName { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }

        public LibraryRange(string name, bool frameworkReference)
        {
            Name = name;

            if (frameworkReference)
            {
                if (Name.IndexOf(FrameworkReferencePrefix) == 0)
                {
                    _frameworkAssemblyName = Name.Substring(FrameworkReferencePrefix.Length);
                }
                else
                {
                    _frameworkAssemblyName = Name;
                    Name = FrameworkReferencePrefix + Name;
                }
            }

            IsGacOrFrameworkReference = frameworkReference;
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

        public string GetReferenceAssemblyName()
        {
            // Assert some things that should NEVER be false.
            Debug.Assert(
                IsGacOrFrameworkReference && Name.StartsWith(FrameworkReferencePrefix) && _frameworkAssemblyName != null,
                "This should only be called on Gac/Framework references");
            return _frameworkAssemblyName;
        }

        public static bool operator ==(LibraryRange left, LibraryRange right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LibraryRange left, LibraryRange right)
        {
            return !Equals(left, right);
        }

        public static string GetAssemblyName(string libraryName)
        {
            if (libraryName.StartsWith(FrameworkReferencePrefix))
            {
                return libraryName.Substring(FrameworkReferencePrefix.Length);
            }
            return libraryName;
        }
    }
}
