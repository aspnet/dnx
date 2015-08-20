// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Dnx.Runtime.Loader;

namespace Microsoft.Dnx.Host
{
    public class DefaultLoadContext : LoadContext
    {
        private readonly LoaderContainer _loaderContainer;

        public DefaultLoadContext(LoaderContainer loaderContainer) : base("Default")
        {
            _loaderContainer = loaderContainer;
        }

        public override Assembly LoadAssembly(AssemblyName assemblyName)
        {
            return _loaderContainer.Load(assemblyName);
        }

        public override IntPtr LoadUnmanagedLibrary(string name)
        {
            return _loaderContainer.LoadUnmanagedLibrary(name);
        }
    }
}