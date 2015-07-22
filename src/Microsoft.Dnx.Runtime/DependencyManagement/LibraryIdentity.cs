// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class LibraryIdentity : IEquatable<LibraryIdentity>
    {
        public string Name { get; set; }

        public SemanticVersion Version { get; set; }

        public bool IsGacOrFrameworkReference { get; set; }

        public override string ToString()
        {
            // NOTE(anurse): We no longer need to put IsGacOrFrameworkReference into the string output because we rename framework dependencies.
            return Name + " " + Version?.ToString();
        }

        public bool Equals(LibraryIdentity other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name) &&
                Equals(Version, other.Version) &&
                Equals(IsGacOrFrameworkReference, other.IsGacOrFrameworkReference);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LibraryIdentity)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^
                    (Version != null ? Version.GetHashCode() : 0) ^
                    (IsGacOrFrameworkReference.GetHashCode());
            }
        }

        public static bool operator ==(LibraryIdentity left, LibraryIdentity right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LibraryIdentity left, LibraryIdentity right)
        {
            return !Equals(left, right);
        }

        public static implicit operator LibraryRange(LibraryIdentity library)
        {
            return new LibraryRange(library.Name, library.IsGacOrFrameworkReference)
            {
                VersionRange = library.Version == null ? null : new SemanticVersionRange
                {
                    MinVersion = library.Version,
                    VersionFloatBehavior = SemanticVersionFloatBehavior.None
                }
            };
        }
    }
}
