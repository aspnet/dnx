// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Runtime;

namespace NuGet
{
    public class PackageDependency
    {
        public PackageDependency(string id, string version)
            : this(id, string.IsNullOrEmpty(version) ? null : VersionUtility.ParseVersionSpec(version))
        {
        }

        public PackageDependency(string id, SemanticVersionRange versionRange)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(nameof(id));
            }

            Id = id;
            VersionSpec = versionRange != null ?
                new VersionSpec
                {
                    IsMinInclusive = true,
                    IsMaxInclusive = versionRange.IsMaxInclusive,
                    MinVersion = versionRange.MinVersion,
                    MaxVersion = versionRange.MaxVersion
                } :
                null;
        }

        public PackageDependency(string id, IVersionSpec versionSpec)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(nameof(id));
            }

            Id = id;
            VersionSpec = versionSpec;
        }

        public string Id { get; }

        public IVersionSpec VersionSpec { get; }

        public override string ToString()
        {
            if (VersionSpec == null)
            {
                return Id;
            }

            return Id + " " + VersionUtility.PrettyPrint(VersionSpec);
        }
    }
}