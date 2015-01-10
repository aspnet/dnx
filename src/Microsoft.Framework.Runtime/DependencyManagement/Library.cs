// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class Library : IEquatable<Library>
    {
        public string Name { get; set; }

        public IVersionSpec RequestedVersion { get; set; }

        public SemanticVersion Version { get; set; }

        public bool IsGacOrFrameworkReference { get; set; }

        public IVersionSpec PreferredRequestedVersion
        {
            get
            {
                if (Version != null)
                {
                    return new VersionSpec(Version);
                }

                return RequestedVersion;
            }
        }

        public SemanticVersion PreferredVersion
        {
            get
            {
                // If there's already a resolved version then perfer that
                if (Version != null)
                {
                    return Version;
                }

                // No idea what the preferred version is for snapshots
                if (RequestedVersion != null && RequestedVersion.IsSnapshot)
                {
                    return null;
                }

                // We always prefer the minimum in a range
                return RequestedVersion?.MinVersion;
            }
        }

        public override string ToString()
        {
            var name = IsGacOrFrameworkReference ? "framework/" + Name : Name;
            return name + " " + (Version?.ToString() ?? RequestedVersion?.ToString());
        }

        public bool Equals(Library other)
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
            return Equals((Library)obj);
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

        public static bool operator ==(Library left, Library right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Library left, Library right)
        {
            return !Equals(left, right);
        }
    }
}
