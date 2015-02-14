// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.LibraryModel
{
    /// <summary>
    /// Represents an entity that can be referenced by a Project, a NuGet Package, or another Project
    /// </summary>
    public class LibraryIdentity : IEquatable<LibraryIdentity>
    {
        public string Name { get; }

        public string Type { get; }

        public NuGetVersion Version { get; }

        public LibraryIdentity(string name, string type, NuGetVersion version)
        {
            Name = name;
            Type = type;
            Version = version;
        }

        public override string ToString()
        {
            var name = Type + "/" + Name;
            if(Version != null)
            {
                name += " " + Version.ToString();
            }
            return name;
        }

        public bool Equals(LibraryIdentity other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name) &&
                Equals(Version, other.Version) &&
                string.Equals(Type, other.Type);
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
                    (Type != null ? Type.GetHashCode() : 0);
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
            return new LibraryRange(
                library.Name,
                library.Type,
                library.Version == null ? null : new NuGetVersionRange
                {
                    MinVersion = library.Version,
                    VersionFloatBehavior = NuGetVersionFloatBehavior.None
                });
        }
    }
}
