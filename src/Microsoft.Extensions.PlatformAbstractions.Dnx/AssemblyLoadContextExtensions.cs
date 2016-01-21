// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;

namespace Microsoft.Extensions.PlatformAbstractions
{
    public static class AssemblyLoadContextExtensions
    {
        public static Assembly Load(this IAssemblyLoadContext loadContext, string name)
        {
            return loadContext.Load(new AssemblyName(name));
        }
    }
}
