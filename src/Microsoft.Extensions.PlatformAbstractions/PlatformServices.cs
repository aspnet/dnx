// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
namespace Microsoft.Extensions.PlatformAbstractions
{
    public abstract class PlatformServices
    {
        private static PlatformServices _defaultPlatformServices;

        public static PlatformServices Default
        {
            get
            {
                return _defaultPlatformServices;
            }
        }

        public abstract IApplicationEnvironment Application { get; }

        public abstract IRuntimeEnvironment Runtime { get; }

        public abstract IAssemblyLoaderContainer AssemblyLoaderContainer { get; }

        public abstract IAssemblyLoadContextAccessor AssemblyLoadContextAccessor { get; }

        public abstract ILibraryManager LibraryManager { get; }

        public static void SetDefault(PlatformServices defaultPlatformServices)
        {
            _defaultPlatformServices = defaultPlatformServices;
        }

        public static PlatformServices Create(
            PlatformServices basePlatformServices,
            IApplicationEnvironment application = null,
            IRuntimeEnvironment runtime = null,
            IAssemblyLoaderContainer container = null,
            IAssemblyLoadContextAccessor accessor = null,
            ILibraryManager libraryManager = null)
        {
            if (basePlatformServices == null)
            {
                return new DefaultPlatformServices(
                    application,
                    runtime,
                    container,
                    accessor,
                    libraryManager
                );
            }
            return new DefaultPlatformServices(
                    application ?? basePlatformServices.Application,
                    runtime ?? basePlatformServices.Runtime,
                    container ?? basePlatformServices.AssemblyLoaderContainer,
                    accessor ?? basePlatformServices.AssemblyLoadContextAccessor,
                    libraryManager ?? basePlatformServices.LibraryManager
                );
        }
    }
}