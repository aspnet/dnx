// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.Framework.Runtime.Dependencies
{
    public interface IFrameworkReferenceResolver
    {
        bool TryGetAssembly(string name, NuGetFramework frameworkName, out string path, out NuGetVersion version);
    }
}