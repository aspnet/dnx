using System;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling
{
    public class WalkProviderMatch : IEquatable<WalkProviderMatch>
    {
        public IWalkProvider Provider { get; set; }
        public LibraryIdentity Library { get; set; }
        public string LibraryType { get; set; }
        public string Path { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as WalkProviderMatch);
        }

        public bool Equals(WalkProviderMatch other)
        {
            // Equality and hash code are based on the identity only
            return other != null && Equals(other.Library, Library);
        }

        public override int GetHashCode()
        {
            // Equality and hash code are based on the identity only
            return Library.GetHashCode();
        }
    }
}