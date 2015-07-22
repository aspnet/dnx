// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public interface IFrameworkReferenceResolver
    {
        bool TryGetAssembly(string name, FrameworkName frameworkName, out string path, out Version version);
    }
}