// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
{
    public class NuGetAssemblyLoader : IAssemblyLoader
    {
        private readonly NuGetDependencyResolver _dependencyResolver;
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public NuGetAssemblyLoader(IAssemblyLoaderEngine loaderEngine, NuGetDependencyResolver dependencyResolver)
        {
            _dependencyResolver = dependencyResolver;
            _loaderEngine = loaderEngine;
        }

        public Assembly Load(string name)
        {
            PackageAssembly assemblyInfo;
            if (_dependencyResolver.PackageAssemblyLookup.TryGetValue(name, out assemblyInfo))
            {
                return _loaderEngine.LoadFile(assemblyInfo.Path);
            }

            return null;
        }
    }
}
