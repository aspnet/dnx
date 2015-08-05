// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling.Utils
{
    public static class PackageResolverHelper
    {
        public static Runtime.Project FindProject(this IProjectResolver resolver, string name)
        {
            Runtime.Project project = null;
            if (resolver.TryResolveProject(name, out project))
            {
                return project;
            }
            else
            {
                return null;
            }
        }
    }
}
