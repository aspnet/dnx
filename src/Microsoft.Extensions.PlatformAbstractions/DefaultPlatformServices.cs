// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Extensions.PlatformAbstractions
{
    internal class DefaultPlatformServices : PlatformServices
    {
        public DefaultPlatformServices()
        {
            Application = new DefaultApplicationEnvironment();
            Runtime = new DefaultRuntimeEnvironment();
        }

        public override IApplicationEnvironment Application { get; }

        public override IRuntimeEnvironment Runtime { get; }

        public override IAssemblyLoaderContainer AssemblyLoaderContainer { get; }

        public override IAssemblyLoadContextAccessor AssemblyLoadContextAccessor { get; }

        public override ILibraryManager LibraryManager { get; }
    }
}