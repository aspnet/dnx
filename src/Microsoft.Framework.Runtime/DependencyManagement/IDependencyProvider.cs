// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public interface IDependencyProvider
    {
        LibraryDescription GetDescription(LibraryRange libraryRange, FrameworkName targetFramework);

        void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework, string runtimeIdentifier);

        IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework);
    }
}
