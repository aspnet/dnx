// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    // We never care about resolving framework references in kpm restore
    internal class EmptyFrameworkResolver : IFrameworkReferenceResolver
    {
        public bool TryGetAssembly(string name, FrameworkName frameworkName, out string path)
        {
            path = null;
            return false;
        }
    }
}