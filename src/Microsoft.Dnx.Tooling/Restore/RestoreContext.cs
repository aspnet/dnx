// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
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
        public IList<RuntimeSpec> RuntimeSpecs { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<LibraryRange, Task<GraphItem>> GraphItemCache { get; private set; }
        public Dictionary<LibraryRange, Task<WalkProviderMatch>> MatchCache { get; set; }
    }
}
