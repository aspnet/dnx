// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Runtime
{
    internal class DefaultPlatformServices : PlatformServices
    {
        internal DefaultPlatformServices(
            IApplicationEnvironment application,
            IRuntimeEnvironment runtime,
            IAssemblyLoaderContainer container,
            IAssemblyLoadContextAccessor accessor,
            ILibraryManager libraryManager)
        {
            Application = application;
            Runtime = runtime;
            AssemblyLoaderContainer = container;
            AssemblyLoadContextAccessor = accessor;
            LibraryManager = libraryManager;
        }


        public override IApplicationEnvironment Application { get; }

        public override IRuntimeEnvironment Runtime { get; }

        public override IAssemblyLoaderContainer AssemblyLoaderContainer { get; }

        public override IAssemblyLoadContextAccessor AssemblyLoadContextAccessor { get; }

        public override ILibraryManager LibraryManager { get; }
    }
}