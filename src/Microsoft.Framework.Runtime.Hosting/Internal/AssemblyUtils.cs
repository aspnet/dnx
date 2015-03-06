// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using NuGet.Versioning;

namespace Microsoft.Framework.Runtime.Internal
{
    internal class AssemblyUtils
    {
        internal static NuGetVersion GetAssemblyVersion(string path)
        {
#if ASPNET50
            return new NuGetVersion(AssemblyName.GetAssemblyName(path).Version);
#else
            return new NuGetVersion(System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path).Version);
#endif
        }
    }
}