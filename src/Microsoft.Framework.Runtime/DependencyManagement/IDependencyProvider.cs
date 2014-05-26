// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public interface IDependencyProvider
    {
        LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework);

        void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework);

        IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework);
    }
}
