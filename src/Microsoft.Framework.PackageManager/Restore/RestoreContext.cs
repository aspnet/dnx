// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreContext
    {
        public RestoreContext()
        {
            FindLibraryCache = new Dictionary<Library, Task<GraphItem>>();
        }

        public FrameworkName FrameworkName { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<Library, Task<GraphItem>> FindLibraryCache { get; private set; }
    }
}
