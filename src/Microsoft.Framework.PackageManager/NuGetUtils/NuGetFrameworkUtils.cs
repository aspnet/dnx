// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using NuGet.Frameworks;

namespace Microsoft.Framework.PackageManager.NuGetUtils
{
    public static class NuGetFrameworkUtils
    {
        public static NuGetFramework ToNuGetFramework(this FrameworkName frameworkName)
        {
            return NuGetFramework.Parse(frameworkName.FullName);
        }
    }
}
