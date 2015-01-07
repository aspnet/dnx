// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Packages;

namespace Microsoft.Framework.PackageManager.Packages
{
    /// <summary>
    /// Summary description for PushOptions
    /// </summary>
    public class PushOptions : PackagesOptions
    {
        public string RemotePackages { get; set; }
    }
}