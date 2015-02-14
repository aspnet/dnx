// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    /// <summary>
    /// Represents a file that describes a package which can be built or referenced
    /// </summary>
    public class PackageSpec
    {
        public PackageSpec()
        {
        }

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public string Description { get; set; }

        public string[] Authors { get; set; }

        public string[] Owners { get; set; }

        public string ProjectUrl { get; set; }

        public string IconUrl { get; set; }

        public string LicenseUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string Copyright { get; set; }

        public string Language { get; set; }

        public string[] Tags { get; set; }

        /// <summary>
        /// Gets any properties that were defined in the original file but were not understood by the parser.
        /// </summary>
        public IDictionary<string, object> Properties { get; }
    }
}
