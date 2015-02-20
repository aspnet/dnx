// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// TODO: Move these classes in to NuGet.Versioning when kproj branch is merged there.

using System;

namespace NuGet.Versioning
{
    public enum NuGetVersionFloatBehavior
    {
        None,
        Prerelease,
        Revision,
        Build,
        Minor,
        Major
    }
}