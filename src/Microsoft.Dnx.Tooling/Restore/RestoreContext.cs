// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;

namespace Microsoft.Dnx.Tooling
{
    public class RestoreContext
    {
        public RestoreContext()
        {
            GraphItemCache = new Dictionary<LibraryRange, Task<GraphItem>>();
            MatchCache = new Dictionary<LibraryRange, Task<WalkProviderMatch>>();
        }

        public FrameworkName FrameworkName { get; set; }

        public string RuntimeName { get; set; }
        public ISet<string> AllRuntimeNames { get; set; }
        public IDictionary<string, DependencySpec> RuntimeDependencies { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<LibraryRange, Task<GraphItem>> GraphItemCache { get; private set; }
        public Dictionary<LibraryRange, Task<WalkProviderMatch>> MatchCache { get; set; }
    }
}
