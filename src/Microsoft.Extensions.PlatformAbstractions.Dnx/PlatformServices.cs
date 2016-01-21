// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Extensions.PlatformAbstractions
{
    public abstract class DnxPlatformServices
    {
        private static DnxPlatformServices _defaultPlatformServices = new DefaultDnxPlatformServices();

        public static DnxPlatformServices Default
        {
            get
            {
                return _defaultPlatformServices;
            }
        }

        public abstract IAssemblyLoaderContainer AssemblyLoaderContainer { get; }

        public abstract IAssemblyLoadContextAccessor AssemblyLoadContextAccessor { get; }

        public abstract ILibraryManager LibraryManager { get; }

        public static void SetDefault(DnxPlatformServices defaultPlatformServices)
        {
            _defaultPlatformServices = defaultPlatformServices;
        }
    }
}
