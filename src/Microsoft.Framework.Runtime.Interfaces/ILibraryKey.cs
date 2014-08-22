// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface ILibraryKey
    {
        string Name { get; }
        FrameworkName TargetFramework { get; }
        string Configuration { get; }
        string Aspect { get; }
    }
}
