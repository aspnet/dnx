// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;
using NuGet.Frameworks;

namespace Microsoft.Framework.Runtime.Hosting.DependencyProviders
{
    public interface IFrameworkReferenceResolver
    {
        bool TryGetAssembly(string name, NuGetFramework frameworkName, out string path);
        string GetFrameworkPath(NuGetFramework targetFramework);
    }
}