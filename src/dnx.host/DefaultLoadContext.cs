// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Microsoft.Framework.Runtime.Loader;

namespace dnx.host
{
    public class DefaultLoadContext : LoadContext
    {
        private readonly LoaderContainer _loaderContainer;

        public DefaultLoadContext(LoaderContainer loaderContainer)
        {
            _loaderContainer = loaderContainer;
        }

        public override Assembly LoadAssembly(AssemblyName name)
        {
            return _loaderContainer.Load(name);
        }
    }
}