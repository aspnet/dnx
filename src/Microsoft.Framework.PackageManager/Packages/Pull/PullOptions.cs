// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Packages;

namespace Microsoft.Framework.PackageManager.Packages
{
    /// <summary>
    /// Summary description for PullOptions
    /// </summary>
    public class PullOptions : PackagesOptions
    {
        public string RemotePackages { get; set; }

        public string RemoteKey { get; set; }
    }
}