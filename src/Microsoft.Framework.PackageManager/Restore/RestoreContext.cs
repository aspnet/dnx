// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using TempRepack.Engine.Model;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreContext
    {
        public RestoreContext()
        {
            FindLibraryCache = new Dictionary<LibraryRange, Task<GraphItem>>();
        }

        public FrameworkName FrameworkName { get; set; }

        public string RuntimeName { get; set; }
        public IList<RuntimeSpec> RuntimeSpecs { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<LibraryRange, Task<GraphItem>> FindLibraryCache { get; private set; }
    }
}
