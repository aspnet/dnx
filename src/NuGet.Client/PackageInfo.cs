// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.Client
{
    public class PackageInfo
    {
        public string Id { get; set; }
        public NuGetVersion Version { get; set; }
        public string ContentUri { get; set; }
        public string ManifestContentUri { get; set; }
    }
}