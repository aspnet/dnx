// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace Microsoft.Framework.Runtime.Loader.NuGet
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

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            string path;
            if (_dependencyResolver.PackageAssemblyPaths.TryGetValue(loadContext.AssemblyName, out path))
            {
                return new AssemblyLoadResult(_loaderEngine.LoadFile(path));
            }

            return null;
        }
    }
}
